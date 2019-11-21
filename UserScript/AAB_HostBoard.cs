using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestSystem.TestLibrary.Algorithms;
using TestSystem.TestLibrary.Instruments;

namespace UserScript
{
  public   class AAB_HostBoard
    {
        readonly double[] referencePower = new double[4] {1.44,-0.45,2.67,0.59 };
        Inst_AAB_HostBoard inst_ABB;
        byte SlaveAdd = 0x50;
        public bool Init(ref string errormessage)
        {
            try
            {
                inst_ABB = new Inst_AAB_HostBoard();
                inst_ABB.EnterEngMod();

                for (int i = 0; i < 4; i++)
                {
                    inst_ABB.SetDCCurrent(i, 0);
                    System.Threading.Thread.Sleep(100);
                    inst_ABB.SetModulationCurrent(i, 0);
                }
                inst_ABB.SetAPCLoop(false);
                inst_ABB.DDM_Enable();
            }
            catch (Exception ex)
            {
                errormessage = ex.Message;
                return false;
            }

            return true;
        }
        public double [] ReadPower()
        {

            UInt16[] ResultRSSI = new UInt16[4];
            for (int i = 0; i < 4; i++)
            {
                byte Page = byte.Parse((0x81 + i).ToString());
                inst_ABB.SelectPage(SlaveAdd, Page);
                byte Data1 = inst_ABB.I2CMasterSingleReadModule(SlaveAdd, 0x92);
                byte Data2 = inst_ABB.I2CMasterSingleReadModule(SlaveAdd, 0x93);
                ResultRSSI[i] = UInt16.Parse((Data1 * 256 + Data2).ToString());
                
            }

            
            double[] Resp = new double[4];

            for (int i = 0; i < 4; i++)
            {
                double Power = Math.Pow(10, referencePower[i] / 10);
                Resp[i] = (double)(ResultRSSI[i]) / 4096.0 * 2.5 / 6.4 * 32;
                Resp[i] = Math.Round(Resp[i] / Power, 2);
            }
            return Resp;

        }
    }
}
