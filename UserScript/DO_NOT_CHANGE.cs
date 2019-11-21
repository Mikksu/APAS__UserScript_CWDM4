using System;
using System.Linq;
using UserScript.Service;
using HalconDotNet;
using System.Drawing;
using System.IO;

namespace UserScript
{
    /// <summary>
    /// =========================== ATTENTION ===========================
    /// ===========================    注意   =========================== 
    /// =                                                               =  
    /// =          Please DO NOT make ANY changes to this file.         =
    /// =                    请勿修改当前文件的任何内容。                   =
    /// =                                                               =
    /// =================================================================
    /// 
    /// </summary>
    partial class APAS_UserScript
    {
        static void Main(string[] args)
        {
            CamRAC.CamRemoteAccessContractClient camClient = new CamRAC.CamRemoteAccessContractClient();
            //camClient.Open();
            //var binBmp = camClient.GrabOneFrame("Rear");
            
            //camClient.Close();

            ////HOperatorSet.ReadImage(out HObject image, "C:\\Users\\user\\Desktop\\awg01.bmp");
            ////HObject2Bpp8(image, out Bitmap bitmap);
            ////Bitmap iii = BitMapZd.DrawCross(bitmap, 100, 100, 30);
            //binBmp.Save("d:\\123.bmp");





            //return;







            var client = new SystemServiceClient();

            try
            {
                client.Open();

                client.__SSC_Connect();

                // perform the user process.
                UserProc(client, camClient);

                client.__SSC_Disonnect();
            }
            catch (AggregateException ae)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.BackgroundColor = ConsoleColor.Red;

                var ex = ae.Flatten();

                ex.InnerExceptions.ToList().ForEach(e =>
                {
                    Console.WriteLine($"Error occurred, {e.Message}");
                });

                Console.ResetColor();
            }
            finally
            {
                client.Close();
            }
            //Console.WriteLine("Press any key to exit.");

            //Console.ReadKey();
        }
    }
}
