// using System;
// using Cysharp.Text;
// using TMPro;
// using UnityEngine;

// /// <summary>
// /// ZString 2.6.0 入门演示脚本（挂到任意 GameObject 上运行，看 Console 输出）。
// /// 覆盖：Concat / Format / Join / Utf16ValueStringBuilder / Utf8ValueStringBuilder / PrepareUtf16 / ZStringWriter / TMP 0GC 更新。
// /// 详细说明见 Docs/ZString使用指南.md
// /// </summary>
// public class ZStringStart : MonoBehaviour
// {
//     /// <summary>拖入场景中的 TextMeshProUGUI（可选），演示第 9 步的 0 GC 更新。</summary>
//     [SerializeField] private TMP_Text hudLabel;

//     void Start()
//     {
//         // 1) 单行拼接：ZString.Concat —— 泛型版本，零装箱零中间分配（仅最终 string 一次分配）
//         string s1 = ZString.Concat("HP: ", 100, "/", 100); // "HP: 100/100"
//         Debug.Log(ZString.Concat("[Concat] ", s1));

//         // 2) 格式化：ZString.Format —— 替代 string.Format，参数不装箱
//         string s2 = ZString.Format("player={0} level={1} exp={2:0.0}", "Bob", 12, 345.678f);
//         Debug.Log(ZString.Concat("[Format] ", s2));

//         // 3) 连接集合：ZString.Join —— 替代 string.Join
//         string s3 = ZString.Join(", ", new[] { 1, 2, 3, 4 }); // "1, 2, 3, 4"
//         Debug.Log(ZString.Concat("[Join] ", s3));

//         // 4) StringBuilder 模式（热路径首选）：零分配构建，using 自动 Dispose 归还池数组
//         using (var sb = ZString.CreateStringBuilder())
//         {
//             sb.Append("击杀 ");
//             sb.Append(10);
//             sb.Append(" 只怪物，获得 ");
//             sb.Append(2500);
//             sb.Append(" 金币");
//             string s4 = sb.ToString(); // 唯一一次分配
//             Debug.Log(ZString.Concat("[StringBuilder] ", s4));
//         }

//         // 5) AppendFormat / AppendJoin
//         using (var sb2 = ZString.CreateStringBuilder())
//         {
//             sb2.AppendFormat("x={0}, y={1}, z={2}", 1, 2, 3);
//             sb2.AppendJoin("-", new[] { "A", "B", "C" }); // 追加 "-A-B-C"
//             Debug.Log(ZString.Concat("[AppendFormat+Join] ", sb2.ToString()));
//         }

//         // 6) UTF-8 构建：直接产出 UTF-8 字节（网络协议/日志/序列化场景）
//         using (var sb8 = ZString.CreateUtf8StringBuilder())
//         {
//             sb8.Append("msg:");
//             sb8.Append(42);
//             ReadOnlySpan<byte> bytes = sb8.AsSpan(); // UTF-8 字节视图，零拷贝
//             Debug.Log(ZString.Concat("[Utf8] byte count = ", bytes.Length, " -> ", sb8.ToString()));
//         }

//         // 7) Prepare 预解析模板：格式串只解析一次，之后反复填充
//         var template = ZString.PrepareUtf16<int, string>("id={0} name={1}");
//         string s7 = template.Format(7, "robot");
//         Debug.Log(ZString.Concat("[PrepareUtf16] ", s7));

//         // 8) ZStringWriter：承接需要 TextWriter 的 API（日志/JSON 序列化等）
//         using (var writer = new ZStringWriter())
//         {
//             writer.Write("level=");
//             writer.Write(99);
//             Debug.Log(ZString.Concat("[ZStringWriter] ", writer.ToString()));
//         }

//         // 9) 与 TextMeshPro 配合：完全 0 GC 更新文本（适合每帧调用，如 HUD/飘字）
//         //    演示一次；实际使用中在 Update 里每帧调用 UpdateTmpLabelZeroGc 即可。
//         UpdateTmpLabelZeroGc(hudLabel, 100, 100);
//     }

//     /// <summary>
//     /// 完全 0 GC 的 TMP 文本更新路径（不经过 ToString，不产生任何中间 string）。
//     ///
//     /// 原理链（均有源码/IL 实证）：
//     ///   1. thread-static 模式 builder（notNested: true）→ 构建过程 0 分配；
//     ///   2. AsArraySegment() → 零拷贝视图，.Array 是底层 char[]（池化缓冲），.Count 是已写长度；
//     ///   3. TMP_Text.SetCharArray → TMP 内部 PopulateTextBackingArray 逐字符拷入自己的缓冲
//     ///      （m_TextBackingArray），全程不产生 string；同步拷贝，返回后即可安全 Dispose builder。
//     ///
//     /// 说明：
//     ///   - thread-static 缓冲首次在线程内分配一次（约 62KB char[]），之后同线程复用 → 稳定后 0 分配；
//     ///   - TMP 内部缓冲首次按文本长度 Resize 一次（一次性分配，之后复用）；
//     ///   - UNITY_EDITOR 下 TMP 还会生成 Inspector 显示用的字符串（#if UNITY_EDITOR），正式构建无此开销；
//     ///   - 不要用 label.text = ...（StringBuilder/ToString 会产生一次 string 分配）。
//     /// </summary>
//     void UpdateTmpLabelZeroGc(TMP_Text label, int hp, int maxHp)
//     {
//         if (label == null) return;

//         using (var sb = ZString.CreateStringBuilder(notNested: true)) // thread-static：0 分配，禁止嵌套
//         {
//             sb.Append("HP: ");
//             sb.Append(hp);
//             sb.Append('/');
//             sb.Append(maxHp);

//             var seg = sb.AsArraySegment();               // 零拷贝：Array=底层缓冲, Count=已写长度
//             label.SetCharArray(seg.Array, 0, seg.Count); // TMP 逐字符拷贝进内部缓冲，无 string 分配
//         } // Dispose：释放线程占用标记（缓冲保留在线程内，下次复用）
//     }
// }
