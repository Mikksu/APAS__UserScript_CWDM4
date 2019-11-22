using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserScript
{
    public class Conditions
    {
        /// <summary>
        /// 耦合电动轴的名称。
        /// </summary>
        public const string ALIGNER = "CWDM4";

        /// <summary>
        /// 控制针筒上下的IO名称。
        /// </summary>
        public const string IO_INJECTOR = "Injector";

        /// <summary>
        /// 视觉对准最小响应度。
        /// 小于该值表示视觉对准失败。
        /// </summary>
        public const double Resp_After_VisionAlign = 1.5;

        /// <summary>
        /// 粗找光完成后的最小响应度。
        /// 小于该值启动粗找光，否则直接启动细找光。
        /// </summary>
        public const double Resp_After_RoughAlign = 2;

        /// <summary>
        /// 细找光后最小响应度。
        /// 细找光后如果响应度小于此值，耦合失败。
        /// </summary>
        public const double Resp_After_AccuracyAlign = 2.2;

        /// <summary>
        /// 角度调整后1-4通道峰值功率的X方向位置误差。
        /// </summary>
        public const double Resp_After_AngleTuning_PeakPosDiff = 1;

        /// <summary>
        /// 最终响应度要求。
        /// </summary>
        public const double Resp_Final = 2.2;

        /// <summary>
        /// 四通道响应度之差的最小要求。
        /// </summary>
        public const double Resp_Final_Diff = 0.5;
    }
}
