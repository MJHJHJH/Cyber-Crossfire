using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 关卡选择 index → YooAsset 场景 location。
    /// </summary>
    public static class LevelSceneLocations
    {
        private static readonly string[] Locations =
        {
            "Level 1",
            "Level 2",
            "Level 3",
            "Level 4",
            "Level 5",
        };

        public static int Count => Locations.Length;

        public static bool TryGet(int index, out string location)
        {
            if (index < 0 || index >= Locations.Length)
            {
                location = null;
                return false;
            }

            location = Locations[index];
            return true;
        }
    }
}
