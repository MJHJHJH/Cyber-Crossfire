using GameFramework;

namespace GamePlay
{
    /// <summary>
    /// FSM 黑板用字符串变量（ReferencePool）。
    /// </summary>
    public sealed class VarString : Variable<string>
    {
        public static VarString Create(string value)
        {
            VarString variable = ReferencePool.Acquire<VarString>();
            variable.Value = value;
            return variable;
        }
    }
}
