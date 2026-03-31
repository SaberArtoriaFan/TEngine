using Herta;

namespace Saber.GAS.RTS.Spatial
{
    /// <summary>
    /// RTS 八叉树空间查询参数。
    /// </summary>
    public sealed class RtsSpatialQueryOptions
    {
        /// <summary>
        /// 创建一份默认查询参数。
        /// </summary>
        public RtsSpatialQueryOptions()
        {
            NodeCapacity = 8;
            MaxDepth = 8;
            BoundsPadding = FP._1;
            MinimumNodeExtent = FP._1;
        }

        /// <summary>
        /// 获取或设置单个叶节点在触发分裂前最多可容纳的单位数。
        /// </summary>
        public int NodeCapacity { get; set; }

        /// <summary>
        /// 获取或设置八叉树最大深度。
        /// </summary>
        public int MaxDepth { get; set; }

        /// <summary>
        /// 获取或设置构建根节点包围盒时附加的边界冗余。
        /// </summary>
        public FP BoundsPadding { get; set; }

        /// <summary>
        /// 获取或设置节点继续细分前允许的最小半尺寸。
        /// </summary>
        public FP MinimumNodeExtent { get; set; }

        /// <summary>
        /// 对参数做最小合法化处理，避免构建过程进入异常值。
        /// </summary>
        internal void Sanitize()
        {
            if (NodeCapacity < 1)
            {
                NodeCapacity = 1;
            }

            if (MaxDepth < 1)
            {
                MaxDepth = 1;
            }

            if (BoundsPadding < FP._0)
            {
                BoundsPadding = FP._0;
            }

            if (MinimumNodeExtent <= FP._0)
            {
                MinimumNodeExtent = FP._1;
            }
        }
    }
}
