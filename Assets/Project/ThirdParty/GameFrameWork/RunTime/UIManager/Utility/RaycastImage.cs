using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RaycastImage : MaskableGraphic
{

    protected RaycastImage()
    {
        useLegacyMeshGeneration = true;
    }
    /// <summary>
    /// 重写网格生成方法，清空顶点填充，不向 GPU 提交任何渲染数据
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); // 彻底清空网格数据
    }
}
