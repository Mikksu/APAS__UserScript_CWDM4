using System.Drawing;
using System.Threading;
using UserScript.Service;
using HalconDotNet;
using CLCamera;
using System;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using UserScript.CamRAC;

namespace UserScript
{
    partial class APAS_UserScript
    {
        #region Variables

        static CameraBase camTop, camDown, camSide, camPd;

        const double awgOriginX = 87901.4;
        const double awgOriginY = 39897.9;
        const double awgOriginAngle = -0.0599751;

        const double pdOriginX = 804.5;
        const double pdOriginY = 1432.5;
        const double pdOriginAngle = -0.0364422;

        const string PM1 = "PM1906A 1";

        const string TEST_IO_REDLIGHT = "红灯";
        const string TEST_IO_YELLIGHT = "黄灯";
        const string TEST_IO_GRELIGHT = "绿灯";

        const string TEST_IO1 = "左后门关";
        static HTuple calibratedata;

        static AAB_HostBoard HostBoard;
        
       

        [DllImport("Kernel32.dll")]
        internal static extern void CopyMemory(IntPtr dest, IntPtr source, IntPtr size);

        #endregion

        #region User Process

        /// <summary>
        /// The section of the user process.
        /// 用户自定义流程函数。
        /// 
        /// Please write your process in the following method.
        /// 请在以下函数中定义您的工艺流程。
        /// 
        /// </summary>
        /// <param name="Service"></param>
        /// <returns></returns>
        static void UserProc(SystemServiceClient Service, CamRemoteAccessContractClient Camera = null)
        {
            string message = "";
            int cycles = 0;
            bool goodAlign = false;

            double awgX = 0, awgY = 0, awgAngle = 0;
            double pdX = 0, pdY = 0, pdAngle = 0;
            double offsetX, offsetY, offsetAngle;

            Camera.SetExposure("AWG", 30000);
            Camera.SetExposure("Left", 38000);

            HOperatorSet.ReadTuple(AppDomain.CurrentDomain.BaseDirectory + "calibratedata.tup", out calibratedata);
            try
            {
                Console.WriteLine("是否开始耦合? Y/[N]");
                var echo = Console.ReadLine();
                if (echo == "Y" || echo == "y")
                {
                    Service.__SSC_LogInfo("移动到AWG角度识别位置...");
                    Service.__SSC_MoveToPresetPosition("CWDM4", "awg拍照位置");
                    Service.__SSC_MoveToPresetPosition("CWDM4", "pd初始角度");
                    Thread.Sleep(100);

                    Service.__SSC_LogInfo("识别AWG角度...");
                    var image1 = Camera.GrabOneFrame("AWG");
                    HObject awgImage;
                    Bitmap2HObjectBpp32(image1, out awgImage);
                    GetAwgOffset(awgImage, ref awgX, ref awgY, ref awgAngle, out Bitmap awgimage);
                    Service.__SSC_ShowImage(awgimage);
                    Service.__SSC_MoveAxis("CWDM4", "R", SSC_MoveMode.REL, 100, -awgAngle);
                    Thread.Sleep(100);

                    Service.__SSC_LogInfo("移动到PD Array角度识别位置...");
                    Service.__SSC_MoveToPresetPosition("CWDM4", "pd拍照位置");

                    Service.__SSC_LogInfo("识别PA Array角度...");
                    var image2 = Camera.GrabOneFrame("Left");
                    HObject pdImage;
                    Bitmap2HObjectBpp32(image2, out pdImage);
                    GetPdOffset(pdImage, ref pdX, ref pdY, ref pdAngle, out Bitmap pdimage);
                    Service.__SSC_ShowImage(pdimage);

                    offsetX = -awgX + pdX;
                    offsetY = -awgY + pdY;

                    Service.__SSC_LogInfo($"x 方向总偏移量 {offsetX}");
                    Service.__SSC_LogInfo($"y 方向总偏移量 {offsetY}");
                    Service.__SSC_MoveToPresetPosition("CWDM4", "耦合高位");
                    Thread.Sleep(100);
                    Service.__SSC_MoveAxis("CWDM4", "X", SSC_MoveMode.REL, 100, offsetX);
                    Thread.Sleep(100);
                    Service.__SSC_MoveAxis("CWDM4", "Y", SSC_MoveMode.REL, 100, offsetY);
                    Thread.Sleep(100);

                    //Console.WriteLine("Press any key to go to 耦合位置.");
                    //Console.ReadKey();
                    //Thread.Sleep(100);

                    Service.__SSC_MoveToPresetPosition("CWDM4", "耦合位置");

                    //Console.WriteLine("Press any key to continue.");
                    //Console.ReadKey();


                    int xmaxpath = 0;
                    double[] data = new double[4];
                    data[0] = Service.__SSC_MeasurableDevice_Read("MercuryHostBoard,0");
                    data[1] = Service.__SSC_MeasurableDevice_Read("MercuryHostBoard,1");
                    data[2] = Service.__SSC_MeasurableDevice_Read("MercuryHostBoard,2");
                    data[3] = Service.__SSC_MeasurableDevice_Read("MercuryHostBoard,3");

                    message = "预对准初始响应度：";
                    for (int i = 0; i < 4; i++)
                    {
                        message += $"[{i}]{data[i]}  ";
                    }
                    Service.__SSC_LogInfo(message);

                    string alignmentProfile = "x&y_roughScan";
                    Service.__SSC_LogInfo($"执行Profile-ND，参数[{alignmentProfile}]...");
                    Service.__SSC_DoProfileND(alignmentProfile);

                    cycles = 0;
                    goodAlign = false;
                    while (cycles < 5)
                    {
                        alignmentProfile = "x&y_detailScan";
                        Service.__SSC_LogInfo($"执行Profile-ND，参数[{alignmentProfile}]，Cycle {cycles + 1}/5...");
                        Service.__SSC_DoProfileND(alignmentProfile);

                        var resp = Service.__SSC_MeasurableDevice_Read("MercuryHostBoard,0");
                        Service.__SSC_LogInfo($"响应度：{resp}");
                        
                        if (resp > 2.8)
                        {
                            goodAlign = true;
                            break;
                        }
                    }

                    if (!goodAlign)
                    {
                        Service.__SSC_LogError("CH1响应度无法达到规格。");
                        return;
                    }


                    cycles = 0;
                    goodAlign = false;
                    while (cycles < 5)
                    {
                        alignmentProfile = "mercury";
                        Service.__SSC_LogInfo($"执行Angle Tuning，参数[{alignmentProfile}]，Cycle {cycles + 1}/5...");
                        var diff = (double)Service.__SSC_DoAngleTuning(alignmentProfile);
                        Service.__SSC_LogInfo($"1-4通道峰值位置误差：{diff.ToString("F3")}um");

                        if (diff < 1)
                        {
                            goodAlign = true;
                            break;
                        }
                    }

                    if (goodAlign)
                        Service.__SSC_LogInfo("耦合完成！");
                    else
                        Service.__SSC_LogError("耦合失败！");

                    //XScan(Service, 30, 3,  xmaxpath);
                    //YScan(Service, 30, 3, xmaxpath);

                    //XScan(Service, 15, 1,  xmaxpath);
                    //YScan(Service, 15, 1, xmaxpath);

                    //XScan(Service, 15, 1,  xmaxpath);
                    //YScan(Service, 15, 1, xmaxpath);

                    //double angle = 0;
                    //AngleAdjust(Service, 30, 1, ref angle);
                    //XScan(Service, 10, 0.5,  xmaxpath);
                    //YScan(Service, 10, 0.5, xmaxpath);


                    // data= HostBoard.ReadPower();


                    //for(int i=0;i<4;i++)
                    //{
                    //    Console.WriteLine($"Channel{i}: " + data[i].ToString()+"\t");
                    //}

                    // Thread.Sleep(3000);
                }
            }
            catch (Exception ex)
            {
                Service.__SSC_LogError(ex.Message);
            }

            System.Threading.Thread.Sleep(100);
        }

        #endregion

        #region Private Methods

        static bool Init()
        {
            try
            {
                camTop = new PylonCamera("CamUp", CameraConnectType.GigEVision2);
                camTop.OpenCamera();
                camTop.SetExpourseTime(50000);
                Console.WriteLine("camUp 相机打开成功");
                camDown = new PylonCamera("CamDown", CameraConnectType.GigEVision2);
                camDown.OpenCamera();
                camDown.SetExpourseTime(30000);
                Console.WriteLine("camDown 相机打开成功");

                string emss = string.Empty;
                HostBoard = new AAB_HostBoard();
                if (!HostBoard.Init(ref emss))
                {
                    Console.WriteLine("功率板初始化失败" + emss);
                    return false;
                }
                Console.WriteLine("功率板初始化成功");
                //  camSide = new PylonCamera("CamSide", CameraConnectType.GigEVision2);
                //   camSide.OpenCamera();
                //  camPd = new PylonCamera("CamPd", CameraConnectType.GigEVision2);
                //  camPd.OpenCamera();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }
        
        static void GetAwgOffset(HObject _image, ref double awgX, ref double awgY, ref double awgAngle, out Bitmap resultImage)
        {
            try
            {
                //      _image = camDown.SnapShot();
                // ShowImage(hDisplay1, _image, null);
                HOperatorSet.Threshold(_image, out HObject region1, 120, 255);
                HOperatorSet.ErosionCircle(region1, out HObject region2, 5.5);
                HOperatorSet.DilationCircle(region2, out HObject region3, 5.5);
                HOperatorSet.Connection(region3, out HObject connectedregins1);
                HOperatorSet.SelectShape(connectedregins1, out HObject selectedregions, "area", "and", 100000, 10000000);
                HOperatorSet.Union1(selectedregions, out HObject regionunion);
                HOperatorSet.ShapeTrans(regionunion, out HObject regiontrans, "rectangle2");
                HOperatorSet.RegionToBin(regiontrans, out HObject _transimage, 255, 0, 3840, 2748);
                HOperatorSet.PointsFoerstner(_transimage, 1, 2, 3, 200, 0.3, "gauss", "false", out HTuple rowJunctions, out HTuple columnJunctions, out HTuple coRRJunctions,
                    out HTuple coRCJunctions, out HTuple coCCJunctions, out HTuple rowArea, out HTuple columnArea, out HTuple coRRArea, out HTuple coRCArea, out HTuple coCCArea);
                if (rowJunctions.Length < 2)
                {
                    throw new Exception("awgCheck error in PointsFoerstner");
                }
                HTuple phi;
                HTuple _angle;
                HTuple _row, _column;
                HOperatorSet.TupleRad(-75, out _angle);
                int hypotenuselength = 730;


                if (columnJunctions[0] < columnJunctions[1])
                {
                    HOperatorSet.AngleLx(rowJunctions[0], columnJunctions[0], rowJunctions[1], columnJunctions[1], out phi);
                    _angle += phi;
                    _row = rowJunctions[1] + Math.Cos(_angle) * hypotenuselength;
                    _column = columnJunctions[1] + Math.Sin(_angle) * hypotenuselength;

                }
                else
                {
                    HOperatorSet.AngleLx(rowJunctions[1], columnJunctions[1], rowJunctions[0], columnJunctions[0], out phi);
                    _angle += phi;
                    _row = rowJunctions[0] + Math.Cos(_angle) * hypotenuselength;
                    _column = columnJunctions[0] + Math.Sin(_angle) * hypotenuselength;

                }
                findline(_image, out HObject line, _row, _column, phi, 150, 150, "nearest_neighbor", 5, 20, "positive", "first");



                double rx = (rowJunctions[0] + rowJunctions[1]) / 2;
                double ry = (columnJunctions[0] + columnJunctions[1]) / 2;

                HOperatorSet.TupleRad(-90, out HTuple _angle90);
                findline(_image, out HObject line1, rx, ry, phi + _angle90, 300, 500, "nearest_neighbor", 2.5, 50, "positive", "first");


                HOperatorSet.GetRegionPoints(line, out HTuple rows1, out HTuple columns1);
                HOperatorSet.TupleLength(rows1, out HTuple count1);

                HOperatorSet.GetRegionPoints(line1, out HTuple rows2, out HTuple columns2);
                HOperatorSet.TupleLength(rows2, out HTuple count2);


                HOperatorSet.IntersectionLines(rows1[0], columns1[0], rows1[count1 - 1], columns1[count1 - 1], rows2[0], columns2[0], rows2[count2 - 1], columns2[count2 - 1], out HTuple finalRow, out HTuple finalColumn, out HTuple isoverlapping);


                HOperatorSet.AffineTransPoint2d(calibratedata, finalRow, finalColumn, out HTuple x, out HTuple y);



                HObject2Bpp8(_image, out Bitmap bitmap);
                resultImage = BitMapZd.DrawCross(bitmap, (float)finalColumn.D, (float)finalRow.D, 45, 30, 10, Color.Red);

                // resultImage = BitMapZd.DrawCircle(bitmap, (float)finalColumn.D, (float)finalRow.D,50,true,10);
                HOperatorSet.WriteImage(_image, "bmp", 0, AppDomain.CurrentDomain.BaseDirectory + "awg.bmp");
                bitmap.Save(AppDomain.CurrentDomain.BaseDirectory + "awg1.bmp");
                resultImage.Save(AppDomain.CurrentDomain.BaseDirectory + "awgresult.bmp");

                awgX = x - awgOriginX;
                awgY = y - awgOriginY;
                //awgX = (_awgX - awgOriginX) / 236 * 200;
                //awgY = (_awgY - awgOriginY) / 236 * 200;
                awgAngle = (awgOriginAngle - phi) * 180 / Math.PI;
                Console.WriteLine("awg x offset: " + awgX.ToString("F6"));
                Console.WriteLine("awg y offset: " + awgY.ToString("F6"));
                Console.WriteLine("awg angle offset: " + awgAngle.ToString("F6"));
                //    RegionX regionX1 = new RegionX(corss, "green");
                //  RegionX regionX2 = new RegionX(arrow, "green");

                //   ShowImage(hDisplay1, _image, new List<RegionX>() { regionX1, regionX2 });
                //    SetTextBox(txt_awgx, awgX.ToString("F6"));
                //     SetTextBox(txt_awgy, awgY.ToString("F6"));
                //    SetTextBox(txt_awgangle, awgAngle.ToString("F6"));
            }
            catch (Exception ex)
            {
                HOperatorSet.WriteImage(_image, "bmp", 0, AppDomain.CurrentDomain.BaseDirectory + "awg.bmp");
                throw ex;


            }
        }
        
        //static  void GetPdOffset(HObject _image,ref double pdX,ref double pdY,ref double pdAngle)

        //{
        //    try
        //    {

        //        _image = camTop.SnapShot();
        //        HOperatorSet.Threshold(_image, out HObject region1, 230, 255);
        //        HOperatorSet.ErosionCircle(region1, out HObject region2, 3.5);
        //        HOperatorSet.Connection(region2, out HObject connectedregins1);
        //        HOperatorSet.SelectShape(connectedregins1, out HObject selectedregions, "area", "and", 130, 99999);
        //        HOperatorSet.Union1(selectedregions, out HObject regionunion);
        //        HOperatorSet.ShapeTrans(regionunion, out HObject regiontrans, "rectangle2");
        //        HOperatorSet.DilationRectangle1(regiontrans, out HObject regionDilation, 151, 151);
        //        HOperatorSet.ReduceDomain(_image, regionDilation, out HObject imagereduced);
        //        HOperatorSet.Threshold(imagereduced, out HObject region3, 80, 255);
        //        HOperatorSet.ErosionCircle(region3, out HObject region4, 5.5);
        //        HOperatorSet.Connection(region4, out HObject connectedregins2);
        //        HOperatorSet.SelectShape(connectedregins2, out HObject selectedregions1, "area", "and", 130, 99999);
        //        HOperatorSet.Union1(selectedregions1, out HObject regionunion1);
        //        HOperatorSet.ShapeTrans(regionunion1, out HObject regiontrans1, "rectangle2");

        //        HOperatorSet.AreaCenter(regiontrans1, out HTuple area, out HTuple row, out HTuple column);
        //        HOperatorSet.RegionFeatures(regiontrans1, "rect2_phi", out HTuple phi);

        //        HOperatorSet.GenCrossContourXld(out HObject corss, row, column, 150, phi - 0.785);
        //        HOperatorSet.TupleCos(phi, out HTuple cos);
        //        HOperatorSet.TupleSin(phi, out HTuple sin);
        //        double x1 = cos * 500 + row;
        //        double y1 = sin * 500 + column;
        //        gen_arrow_contour_xld(out HObject arrow, row, column, x1, y1, 20, 20);
        //        pdX = (pdOriginX - row) / 236 * 200;
        //        pdY = (pdOriginY - column) / 236 * 200;
        //        pdAngle = (pdOriginAngle - phi) * 180 / Math.PI;

        //        Console.WriteLine("pd x offset: " + pdX.ToString("F6"));
        //        Console.WriteLine("pd y offset: " + pdY.ToString("F6"));
        //        Console.WriteLine("pd angle offset: " + pdAngle.ToString("F6"));
        //        //  RegionX regionX1 = new RegionX(corss, "green");
        //        //  RegionX regionX2 = new RegionX(arrow, "green");

        //        //   ShowImage(hDisplay1, _image, new List<RegionX>() { regionX1, regionX2 });
        //        //   SetTextBox(txt_pdx, pdX.ToString("F6"));
        //        //    SetTextBox(txt_pdy, pdY.ToString("F6"));
        //        //    SetTextBox(txt_pdangle, pdAngle.ToString("F6"));

        //        //     offsetX = awgX + pdX;
        //        //     offsetY = awgY + pdY;

        //        //     SetTextBox(txt_x, offsetX.ToString("F6"));
        //        //     SetTextBox(txt_y, offsetY.ToString("F6"));

        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //      //  MessageBox.Show(ex.Message);
        //        HOperatorSet.WriteImage(_image, "bmp", 0, AppDomain.CurrentDomain.BaseDirectory + "pd.bmp");
        //    }
        //}

        static void GetPdOffset(HObject _image, ref double pdX, ref double pdY, ref double pdAngle, out Bitmap pdresult)
        {
            try
            {

                //     _image = camTop.SnapShot();
                HObject ho_Region, ho_RegionErosion;
                HObject ho_ConnectedRegions, ho_SelectedRegions, ho_RegionDilation;
                HObject ho_RegionFillUp, ho_ImageReduced, ho_Region1, ho_RegionErosion1;
                HObject ho_ConnectedRegions1, ho_SelectedRegions1, ho_RegionUnion;
                HObject ho_RegionTrans, ho_ImageReduced1, ho_Region2, ho_RegionFillUp1;
                HObject ho_ImageReduced2, ho_Region3, ho_RegionErosion2;
                HObject ho_ConnectedRegions2, ho_SelectedRegions2, ho_RegionTrans1;
                HObject ho_RegionUnion1, ho_RegionTrans2, ho_Cross;

                // Local control variables 

                HTuple hv_Area = null, hv_Row = null, hv_Column = null;
                HTuple hv_Value = null;
                // Initialize local and output iconic variables 

                HOperatorSet.GenEmptyObj(out ho_Region);
                HOperatorSet.GenEmptyObj(out ho_RegionErosion);
                HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
                HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
                HOperatorSet.GenEmptyObj(out ho_RegionDilation);
                HOperatorSet.GenEmptyObj(out ho_RegionFillUp);
                HOperatorSet.GenEmptyObj(out ho_ImageReduced);
                HOperatorSet.GenEmptyObj(out ho_Region1);
                HOperatorSet.GenEmptyObj(out ho_RegionErosion1);
                HOperatorSet.GenEmptyObj(out ho_ConnectedRegions1);
                HOperatorSet.GenEmptyObj(out ho_SelectedRegions1);
                HOperatorSet.GenEmptyObj(out ho_RegionUnion);
                HOperatorSet.GenEmptyObj(out ho_RegionTrans);
                HOperatorSet.GenEmptyObj(out ho_ImageReduced1);
                HOperatorSet.GenEmptyObj(out ho_Region2);
                HOperatorSet.GenEmptyObj(out ho_RegionFillUp1);
                HOperatorSet.GenEmptyObj(out ho_ImageReduced2);
                HOperatorSet.GenEmptyObj(out ho_Region3);
                HOperatorSet.GenEmptyObj(out ho_RegionErosion2);
                HOperatorSet.GenEmptyObj(out ho_ConnectedRegions2);
                HOperatorSet.GenEmptyObj(out ho_SelectedRegions2);
                HOperatorSet.GenEmptyObj(out ho_RegionTrans1);
                HOperatorSet.GenEmptyObj(out ho_RegionUnion1);
                HOperatorSet.GenEmptyObj(out ho_RegionTrans2);
                HOperatorSet.GenEmptyObj(out ho_Cross);

                ho_Region.Dispose();
                HOperatorSet.Threshold(_image, out ho_Region, 0, 155);
                ho_RegionErosion.Dispose();
                HOperatorSet.ErosionCircle(ho_Region, out ho_RegionErosion, 6.5);
                ho_ConnectedRegions.Dispose();
                HOperatorSet.Connection(ho_RegionErosion, out ho_ConnectedRegions);
                ho_SelectedRegions.Dispose();
                HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_SelectedRegions, "area",
                    "and", 500000, 99999999);
                ho_RegionDilation.Dispose();
                HOperatorSet.DilationCircle(ho_SelectedRegions, out ho_RegionDilation, 3.5);
                ho_RegionFillUp.Dispose();
                HOperatorSet.FillUpShape(ho_RegionDilation, out ho_RegionFillUp, "area", 1, 150000);
                ho_ImageReduced.Dispose();
                HOperatorSet.ReduceDomain(_image, ho_RegionFillUp, out ho_ImageReduced);
                ho_Region1.Dispose();
                HOperatorSet.Threshold(ho_ImageReduced, out ho_Region1, 240, 255);
                ho_RegionErosion1.Dispose();
                HOperatorSet.ErosionCircle(ho_Region1, out ho_RegionErosion1, 5.5);
                ho_ConnectedRegions1.Dispose();
                HOperatorSet.Connection(ho_RegionErosion1, out ho_ConnectedRegions1);
                ho_SelectedRegions1.Dispose();
                HOperatorSet.SelectShape(ho_ConnectedRegions1, out ho_SelectedRegions1, "area",
                    "and", 500, 99999);
                ho_RegionUnion.Dispose();
                HOperatorSet.Union1(ho_SelectedRegions1, out ho_RegionUnion);
                ho_RegionTrans.Dispose();
                HOperatorSet.ShapeTrans(ho_RegionUnion, out ho_RegionTrans, "rectangle2");

                ho_ImageReduced1.Dispose();
                HOperatorSet.ReduceDomain(ho_ImageReduced, ho_RegionTrans, out ho_ImageReduced1
                    );
                ho_Region2.Dispose();
                HOperatorSet.Threshold(ho_ImageReduced1, out ho_Region2, 128, 255);
                ho_RegionFillUp1.Dispose();
                HOperatorSet.FillUp(ho_Region2, out ho_RegionFillUp1);
                ho_ImageReduced2.Dispose();
                HOperatorSet.ReduceDomain(ho_ImageReduced1, ho_RegionFillUp1, out ho_ImageReduced2
                    );
                ho_Region3.Dispose();
                HOperatorSet.Threshold(ho_ImageReduced2, out ho_Region3, 0, 128);
                ho_RegionErosion2.Dispose();
                HOperatorSet.ErosionCircle(ho_Region3, out ho_RegionErosion2, 1.5);
                ho_ConnectedRegions2.Dispose();
                HOperatorSet.Connection(ho_RegionErosion2, out ho_ConnectedRegions2);
                ho_SelectedRegions2.Dispose();
                HOperatorSet.SelectShape(ho_ConnectedRegions2, out ho_SelectedRegions2, "area",
                    "and", 100, 1000);
                ho_RegionTrans1.Dispose();
                HOperatorSet.ShapeTrans(ho_SelectedRegions2, out ho_RegionTrans1, "outer_circle");

                ho_RegionUnion1.Dispose();
                HOperatorSet.Union1(ho_RegionTrans1, out ho_RegionUnion1);
                ho_RegionTrans2.Dispose();
                HOperatorSet.ShapeTrans(ho_RegionUnion1, out ho_RegionTrans2, "rectangle2");

                HOperatorSet.AreaCenter(ho_RegionTrans2, out hv_Area, out hv_Row, out hv_Column);
                ho_Cross.Dispose();
                //  HOperatorSet.GenCrossContourXld(out ho_Cross, hv_Row, hv_Column, 50, 0.785398);

                HOperatorSet.RegionFeatures(ho_RegionTrans2, "rect2_phi", out hv_Value);
                //  hv_Value = (hv_Value * 180) / 3.1415926;

                HObject2Bpp8(_image, out Bitmap bitmap);
                pdresult = BitMapZd.DrawCross(bitmap, (float)hv_Column.D, (float)hv_Row.D, 45, 30, 10, Color.Red);


                pdX = (pdOriginX - hv_Row) / 236 * 200;
                pdY = (pdOriginY - hv_Column) / 236 * 200;
                pdAngle = (pdOriginAngle - hv_Value) * 180 / Math.PI;

                Console.WriteLine("pd x offset: " + pdX.ToString("F6"));
                Console.WriteLine("pd y offset: " + pdY.ToString("F6"));
                Console.WriteLine("pd angle offset: " + pdAngle.ToString("F6"));


                ho_Region.Dispose();
                ho_RegionErosion.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();
                ho_RegionDilation.Dispose();
                ho_RegionFillUp.Dispose();
                ho_ImageReduced.Dispose();
                ho_Region1.Dispose();
                ho_RegionErosion1.Dispose();
                ho_ConnectedRegions1.Dispose();
                ho_SelectedRegions1.Dispose();
                ho_RegionUnion.Dispose();
                ho_RegionTrans.Dispose();
                ho_ImageReduced1.Dispose();
                ho_Region2.Dispose();
                ho_RegionFillUp1.Dispose();
                ho_ImageReduced2.Dispose();
                ho_Region3.Dispose();
                ho_RegionErosion2.Dispose();
                ho_ConnectedRegions2.Dispose();
                ho_SelectedRegions2.Dispose();
                ho_RegionTrans1.Dispose();
                ho_RegionUnion1.Dispose();
                ho_RegionTrans2.Dispose();
                ho_Cross.Dispose();
                //  RegionX regionX1 = new RegionX(corss, "green");
                //  RegionX regionX2 = new RegionX(arrow, "green");

                //   ShowImage(hDisplay1, _image, new List<RegionX>() { regionX1, regionX2 });
                //   SetTextBox(txt_pdx, pdX.ToString("F6"));
                //    SetTextBox(txt_pdy, pdY.ToString("F6"));
                //    SetTextBox(txt_pdangle, pdAngle.ToString("F6"));

                //     offsetX = awgX + pdX;
                //     offsetY = awgY + pdY;

                //     SetTextBox(txt_x, offsetX.ToString("F6"));
                //     SetTextBox(txt_y, offsetY.ToString("F6"));

            }
            catch (Exception ex)
            {
                HOperatorSet.WriteImage(_image, "bmp", 0, AppDomain.CurrentDomain.BaseDirectory + "pd.bmp");

                throw ex;
                //  MessageBox.Show(ex.Message);
            }
        }

        static void gen_arrow_contour_xld(
            out HObject ho_Arrow, 
            HTuple hv_Row1, 
            HTuple hv_Column1, 
            HTuple hv_Row2, 
            HTuple hv_Column2, 
            HTuple hv_HeadLength, 
            HTuple hv_HeadWidth)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_TempArrow = null;

            // Local control variables 

            HTuple hv_Length = null, hv_ZeroLengthIndices = null;
            HTuple hv_DR = null, hv_DC = null, hv_HalfHeadWidth = null;
            HTuple hv_RowP1 = null, hv_ColP1 = null, hv_RowP2 = null;
            HTuple hv_ColP2 = null, hv_Index = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            HOperatorSet.GenEmptyObj(out ho_TempArrow);
            //This procedure generates arrow shaped XLD contours,
            //pointing from (Row1, Column1) to (Row2, Column2).
            //If starting and end point are identical, a contour consisting
            //of a single point is returned.
            //
            //input parameteres:
            //Row1, Column1: Coordinates of the arrows' starting points
            //Row2, Column2: Coordinates of the arrows' end points
            //HeadLength, HeadWidth: Size of the arrow heads in pixels
            //
            //output parameter:
            //Arrow: The resulting XLD contour
            //
            //The input tuples Row1, Column1, Row2, and Column2 have to be of
            //the same length.
            //HeadLength and HeadWidth either have to be of the same length as
            //Row1, Column1, Row2, and Column2 or have to be a single element.
            //If one of the above restrictions is violated, an error will occur.
            //
            //
            //Init
            ho_Arrow.Dispose();
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            //
            //Calculate the arrow length
            HOperatorSet.DistancePp(hv_Row1, hv_Column1, hv_Row2, hv_Column2, out hv_Length);
            //
            //Mark arrows with identical start and end point
            //(set Length to -1 to avoid division-by-zero exception)
            hv_ZeroLengthIndices = hv_Length.TupleFind(0);
            if ((int)(new HTuple(hv_ZeroLengthIndices.TupleNotEqual(-1))) != 0)
            {
                if (hv_Length == null)
                    hv_Length = new HTuple();
                hv_Length[hv_ZeroLengthIndices] = -1;
            }
            //
            //Calculate auxiliary variables.
            hv_DR = (1.0 * (hv_Row2 - hv_Row1)) / hv_Length;
            hv_DC = (1.0 * (hv_Column2 - hv_Column1)) / hv_Length;
            hv_HalfHeadWidth = hv_HeadWidth / 2.0;
            //
            //Calculate end points of the arrow head.
            hv_RowP1 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) + (hv_HalfHeadWidth * hv_DC);
            hv_ColP1 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) - (hv_HalfHeadWidth * hv_DR);
            hv_RowP2 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) - (hv_HalfHeadWidth * hv_DC);
            hv_ColP2 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) + (hv_HalfHeadWidth * hv_DR);
            //
            //Finally create output XLD contour for each input point pair
            for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_Length.TupleLength())) - 1); hv_Index = (int)hv_Index + 1)
            {
                if ((int)(new HTuple(((hv_Length.TupleSelect(hv_Index))).TupleEqual(-1))) != 0)
                {
                    //Create_ single points for arrows with identical start and end point
                    ho_TempArrow.Dispose();
                    HOperatorSet.GenContourPolygonXld(out ho_TempArrow, hv_Row1.TupleSelect(hv_Index),
                        hv_Column1.TupleSelect(hv_Index));
                }
                else
                {
                    //Create arrow contour
                    ho_TempArrow.Dispose();
                    HOperatorSet.GenContourPolygonXld(out ho_TempArrow, ((((((((((hv_Row1.TupleSelect(
                        hv_Index))).TupleConcat(hv_Row2.TupleSelect(hv_Index)))).TupleConcat(
                        hv_RowP1.TupleSelect(hv_Index)))).TupleConcat(hv_Row2.TupleSelect(hv_Index)))).TupleConcat(
                        hv_RowP2.TupleSelect(hv_Index)))).TupleConcat(hv_Row2.TupleSelect(hv_Index)),
                        ((((((((((hv_Column1.TupleSelect(hv_Index))).TupleConcat(hv_Column2.TupleSelect(
                        hv_Index)))).TupleConcat(hv_ColP1.TupleSelect(hv_Index)))).TupleConcat(
                        hv_Column2.TupleSelect(hv_Index)))).TupleConcat(hv_ColP2.TupleSelect(
                        hv_Index)))).TupleConcat(hv_Column2.TupleSelect(hv_Index)));
                }
                {
                    HObject ExpTmpOutVar_0;
                    HOperatorSet.ConcatObj(ho_Arrow, ho_TempArrow, out ExpTmpOutVar_0);
                    ho_Arrow.Dispose();
                    ho_Arrow = ExpTmpOutVar_0;
                }
            }
            ho_TempArrow.Dispose();

            return;
        }

        public static void findline(
            HObject ho_Image, 
            out HObject ho_Line, 
            HTuple hv_Rectangle2Row,
            HTuple hv_Rectangle2Column, 
            HTuple hv_Rectangle2Phi, 
            HTuple hv_Rectangle2Length1,
            HTuple hv_Rectangle2Length2, 
            HTuple hv_Interpolation, 
            HTuple hv_Sigma, 
            HTuple hv_Threshold,
            HTuple hv_Transition, 
            HTuple hv_Select)
        {




            // Local iconic variables 

            HObject ho_Cross3 = null, ho_Contour;

            // Local control variables 

            HTuple hv_Width = null, hv_Height = null, hv_Cos = null;
            HTuple hv_Sin = null, hv_row1 = null, hv_column1 = null;
            HTuple hv_row2 = null, hv_column2 = null, hv_pointsX = null;
            HTuple hv_pointsY = null, hv_Function = new HTuple(), hv_num = new HTuple();
            HTuple hv_Index = new HTuple(), hv_x = new HTuple(), hv_y = new HTuple();
            HTuple hv_MeasureHandle = new HTuple(), hv_RowEdge = new HTuple();
            HTuple hv_ColumnEdge = new HTuple(), hv_Amplitude = new HTuple();
            HTuple hv_Distance = new HTuple(), hv_RowBegin = null;
            HTuple hv_ColBegin = null, hv_RowEnd = null, hv_ColEnd = null;
            HTuple hv_Nr = null, hv_Nc = null, hv_Dist = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Line);
            HOperatorSet.GenEmptyObj(out ho_Cross3);
            HOperatorSet.GenEmptyObj(out ho_Contour);
            try
            {
                HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);

                HOperatorSet.TupleCos(hv_Rectangle2Phi, out hv_Cos);
                HOperatorSet.TupleSin(hv_Rectangle2Phi, out hv_Sin);
                hv_row1 = hv_Rectangle2Row - (hv_Rectangle2Length2 * hv_Cos);
                hv_column1 = hv_Rectangle2Column - (hv_Rectangle2Length2 * hv_Sin);
                hv_row2 = hv_Rectangle2Row + (hv_Rectangle2Length2 * hv_Cos);
                hv_column2 = hv_Rectangle2Column + (hv_Rectangle2Length2 * hv_Sin);
                hv_pointsX = new HTuple();
                hv_pointsY = new HTuple();
                if ((int)((new HTuple(((hv_Rectangle2Phi.TupleAbs())).TupleGreater(0.785))).TupleAnd(
                    new HTuple(((hv_Rectangle2Phi.TupleAbs())).TupleLess(2.345)))) != 0)
                {
                    if ((int)(new HTuple(hv_column1.TupleLess(hv_column2))) != 0)
                    {

                        HOperatorSet.CreateFunct1dPairs(hv_column1.TupleConcat(hv_column2), hv_row1.TupleConcat(
                            hv_row2), out hv_Function);
                    }
                    else
                    {
                        HOperatorSet.CreateFunct1dPairs(hv_column2.TupleConcat(hv_column1), hv_row2.TupleConcat(
                            hv_row1), out hv_Function);
                    }
                    hv_num = ((hv_Rectangle2Length2 / 3)).TupleInt();
                    HTuple end_val18 = hv_num;
                    HTuple step_val18 = 1;
                    for (hv_Index = 1; hv_Index.Continue(end_val18, step_val18); hv_Index = hv_Index.TupleAdd(step_val18))
                    {
                        if ((int)(new HTuple(hv_Index.TupleEqual(1))) != 0)
                        {
                            hv_x = (hv_column1.TupleMin2(hv_column2)) + (((((hv_column1 - hv_column2)).TupleAbs()
                                ) / hv_num) / 2);
                        }
                        else
                        {
                            hv_x = (hv_column1.TupleMin2(hv_column2)) + (((((hv_column1 - hv_column2)).TupleAbs()
                                ) / hv_num) * (hv_Index - 0.5));
                        }
                        HOperatorSet.GetYValueFunct1d(hv_Function, hv_x, "constant", out hv_y);

                        //gen_cross_contour_xld (Cross2, x, y, 6, Phi)


                        //gen_rectangle2 (Rectangle1, y, x, Phi, Length1, abs(row1-row2)/num/2)
                        HOperatorSet.GenMeasureRectangle2(hv_y, hv_x, hv_Rectangle2Phi, hv_Rectangle2Length1,
                            ((((hv_column1 - hv_column2)).TupleAbs()) / hv_num) / 2, hv_Width, hv_Height,
                            hv_Interpolation, out hv_MeasureHandle);
                        HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, hv_Sigma, hv_Threshold,
                            hv_Transition, hv_Select, out hv_RowEdge, out hv_ColumnEdge, out hv_Amplitude,
                            out hv_Distance);
                        ho_Cross3.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross3, hv_RowEdge, hv_ColumnEdge,
                            10, hv_Rectangle2Phi);
                        HOperatorSet.CloseMeasure(hv_MeasureHandle);
                        hv_pointsX = hv_pointsX.TupleConcat(hv_RowEdge);
                        hv_pointsY = hv_pointsY.TupleConcat(hv_ColumnEdge);
                    }


                }
                else
                {
                    if ((int)(new HTuple(hv_row1.TupleLess(hv_row2))) != 0)
                    {

                        HOperatorSet.CreateFunct1dPairs(hv_row1.TupleConcat(hv_row2), hv_column1.TupleConcat(
                            hv_column2), out hv_Function);
                    }
                    else
                    {
                        HOperatorSet.CreateFunct1dPairs(hv_row2.TupleConcat(hv_row1), hv_column2.TupleConcat(
                            hv_column1), out hv_Function);
                    }

                    hv_num = ((hv_Rectangle2Length2 / 3)).TupleInt();

                    HTuple end_val49 = hv_num;
                    HTuple step_val49 = 1;
                    for (hv_Index = 1; hv_Index.Continue(end_val49, step_val49); hv_Index = hv_Index.TupleAdd(step_val49))
                    {
                        if ((int)(new HTuple(hv_Index.TupleEqual(1))) != 0)
                        {
                            hv_x = (hv_row1.TupleMin2(hv_row2)) + ((((((hv_row1 - hv_row2)).TupleAbs()
                                ) / hv_num) / 2) * hv_Index);
                        }
                        else
                        {
                            hv_x = (hv_row1.TupleMin2(hv_row2)) + (((((hv_row1 - hv_row2)).TupleAbs()
                                ) / hv_num) * (hv_Index - 0.5));
                        }
                        HOperatorSet.GetYValueFunct1d(hv_Function, hv_x, "constant", out hv_y);

                        //gen_cross_contour_xld (Cross2, x, y, 6, Phi)


                        //gen_rectangle2 (Rectangle1, x, y, Phi, Length1, abs(row1-row2)/num/2)
                        HOperatorSet.GenMeasureRectangle2(hv_x, hv_y, hv_Rectangle2Phi, hv_Rectangle2Length1,
                            ((((hv_row1 - hv_row2)).TupleAbs()) / hv_num) / 2, hv_Width, hv_Height, hv_Interpolation,
                            out hv_MeasureHandle);
                        HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, hv_Sigma, hv_Threshold,
                            hv_Transition, hv_Select, out hv_RowEdge, out hv_ColumnEdge, out hv_Amplitude,
                            out hv_Distance);
                        ho_Cross3.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross3, hv_RowEdge, hv_ColumnEdge,
                            10, hv_Rectangle2Phi);
                        HOperatorSet.CloseMeasure(hv_MeasureHandle);
                        hv_pointsX = hv_pointsX.TupleConcat(hv_RowEdge);
                        hv_pointsY = hv_pointsY.TupleConcat(hv_ColumnEdge);
                    }
                }

                ho_Contour.Dispose();
                HOperatorSet.GenContourPolygonXld(out ho_Contour, hv_pointsX, hv_pointsY);
                HOperatorSet.FitLineContourXld(ho_Contour, "tukey", -1, 0, 5, 2, out hv_RowBegin,
                    out hv_ColBegin, out hv_RowEnd, out hv_ColEnd, out hv_Nr, out hv_Nc, out hv_Dist);
                ho_Line.Dispose();
                HOperatorSet.GenRegionLine(out ho_Line, hv_RowBegin, hv_ColBegin, hv_RowEnd,
                    hv_ColEnd);

                ho_Cross3.Dispose();
                ho_Contour.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Cross3.Dispose();
                ho_Contour.Dispose();

                throw HDevExpDefaultException;
            }
        }
       
        /// <summary>
        /// x方向查找最大值
        /// </summary>
        /// <param name="Service"></param>
        /// <param name="halflenth">查找范围的二分之一长度</param>
        /// <param name="offset">每次的偏移量</param>
        private static void XScan(SystemServiceClient Service, uint halflenth, double offset, int maxpath)
        {

            try
            {
                Console.WriteLine("XScan");

                Service.__SSC_MoveAxis("CWDM4", "X", SSC_MoveMode.REL, 100, -halflenth);
                double number = halflenth / offset * 2;
                double[,] data = new double[(int)number, 4];
                int MaxDataIndex = 0;
                double MaxData = 0;
                for (int i = 0; i < number; i++)
                {
                    Service.__SSC_MoveAxis("CWDM4", "X", SSC_MoveMode.REL, 100, offset);
                    ///读取功率计的值
                    ///比较四个通道的读值，选择功率最大的，
                    ///
                    double[] d = HostBoard.ReadPower();

                    Console.Write(d[maxpath].ToString() + "   ");

                    Console.WriteLine("\r\n");
                    if (MaxData < d[maxpath])
                    {
                        MaxData = d[maxpath];
                        MaxDataIndex = i;
                    }

                }
                Service.__SSC_MoveAxis("CWDM4", "X", SSC_MoveMode.REL, 100, -offset * (number - MaxDataIndex));//移动到最大值处

            }
            catch (Exception ex)
            {

            }
        }
       
        /// <summary>
        /// Y方向查找最大值
        /// </summary>
        static void YScan(SystemServiceClient Service, uint halflenth, double offset, int maxpath)
        {
            try
            {
                Console.WriteLine("YScan");
                Service.__SSC_MoveAxis("CWDM4", "Y", SSC_MoveMode.REL, 100, -halflenth);
                double number = halflenth / offset * 2;
                double[,] data = new double[(int)number, 4];
                int MaxDataIndex = 0;
                double MaxData = 0;
                for (int i = 0; i < number; i++)
                {
                    Service.__SSC_MoveAxis("CWDM4", "Y", SSC_MoveMode.REL, 100, offset);
                    ///读取功率计的值

                    double[] d = HostBoard.ReadPower();

                    Console.Write(d[maxpath].ToString() + "   ");

                    Console.WriteLine("\r\n");
                    if (MaxData < d[maxpath])
                    {
                        MaxData = d[maxpath];
                        MaxDataIndex = i;
                    }
                }
                Service.__SSC_MoveAxis("CWDM4", "Y", SSC_MoveMode.REL, 100, -offset * (number - MaxDataIndex));//移动到最大值处
            }
            catch (Exception ex)
            {

            }
        }
       
        /// <summary>
        /// 调整pd角度
        /// </summary>
        static void AngleAdjust(SystemServiceClient Service, uint halflenth, uint offset, ref double angle)
        {
            try
            {
                Console.WriteLine("开始角度补偿");
                Service.__SSC_MoveAxis("CWDM4", "X", SSC_MoveMode.REL, 100, -halflenth);
                uint number = halflenth / offset * 2;
                double[,] data = new double[number, 4];
                int FirstPathMaxDataIndex = 0;//第一通道最大值的index
                int ForthPathMaxDataIndex = 0;//第四通道最大值的index
                double FirstPathMaxData;
                double ForthPathMaxData;
                for (int i = 0; i < number; i++)
                {
                    Service.__SSC_MoveAxis("CWDM4", "X", SSC_MoveMode.REL, 100, offset);
                    ///读取功率计的值
                    double[] d = HostBoard.ReadPower();
                    data[i, 0] = d[0];
                    data[i, 1] = d[1];
                    data[i, 2] = d[2];
                    data[i, 3] = d[3];
                }
                double pathMaxData1 = 0;
                double pathMaxData2 = 0;
                for (int j = 0; j < number; j++)
                {
                    if (pathMaxData1 < data[j, 0])
                    {
                        pathMaxData1 = data[j, 0];
                        FirstPathMaxDataIndex = j;
                    }
                    if (pathMaxData2 < data[j, 3])
                    {
                        pathMaxData2 = data[j, 3];
                        ForthPathMaxDataIndex = j;
                    }
                }
                angle = Math.Asin((FirstPathMaxDataIndex - ForthPathMaxDataIndex) * offset / 750.00) / Math.PI * 180;
                Console.WriteLine($"peakDiff：" + (FirstPathMaxDataIndex - ForthPathMaxDataIndex));

                Console.WriteLine($"角度预测：" + angle);
                Service.__SSC_MoveAxis("CWDM4", "X", SSC_MoveMode.REL, 100, -offset * (number - (FirstPathMaxDataIndex + ForthPathMaxDataIndex) / 2));//移动到最大值处

                Service.__SSC_MoveAxis("CWDM4", "R", SSC_MoveMode.REL, 100, -angle);//移动到最大值处

            }
            catch (Exception ex)
            {

            }
        }
        
        static void HObject2Bpp8(HObject image, out Bitmap res)
        {
            HTuple hpoint, type, width, height;
            const int Alpha = 255;
            IntPtr[] ptr = new IntPtr[2];
            HOperatorSet.GetImagePointer1(image, out hpoint, out type, out width, out height);
            res = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            ColorPalette pal = res.Palette;
            for (int i = 0; i <= 255; i++)
            {
                pal.Entries[i] = Color.FromArgb(Alpha, i, i, i);
            }



            res.Palette = pal;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = res.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            int PixelSize = Bitmap.GetPixelFormatSize(bmpData.PixelFormat) / 8;
            ptr[0] = bmpData.Scan0;
            ptr[1] = (IntPtr)hpoint.L;
            if (width % 4 == 0)
                CopyMemory(ptr[0], ptr[1], width * height * PixelSize);
            else
            {
                for (int i = 0; i < height - 1; i++)
                {
                    ptr[1] += width.I;

                    CopyMemory(ptr[0], ptr[1], width * PixelSize);
                    ptr[0] += bmpData.Stride;
                }
            }
            res.UnlockBits(bmpData);
        }

        static void Bitmap2HObjectBpp32(Bitmap bmp, out HObject image)
        {
            try
            {
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);

                BitmapData srcBmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                HOperatorSet.GenImageInterleaved(out image, srcBmpData.Scan0, "rgbx", bmp.Width, bmp.Height, 0, "byte", 0, 0, 0, 0, -1, 0);
                bmp.UnlockBits(srcBmpData);
            }
            catch (Exception ex)
            {
                throw new Exception($"unable to convert bitmap to hobject, {ex.Message}");
            }
        }

        #endregion

    }
}
