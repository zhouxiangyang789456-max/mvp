#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mvp.Battle.Units;

namespace Mvp.EditorTests.Battle.Units
{
    public sealed class UnitMovementPathReuseTests
    {
        [Test]
        public void 已验证路径首尾匹配时可以复用()
        {
            var path = new List<Vector2Int>
            {
                new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1)
            };
            Assert.IsTrue(UnitMovementController.IsReusablePath(path,
                new Vector2Int(1, 1), new Vector2Int(3, 1)));
        }

        [Test]
        public void 起点不匹配时拒绝复用()
        {
            var path = new List<Vector2Int>
            {
                new Vector2Int(1, 1), new Vector2Int(2, 1)
            };
            Assert.IsFalse(UnitMovementController.IsReusablePath(path,
                Vector2Int.zero, new Vector2Int(2, 1)));
        }

        [Test]
        public void 终点不匹配时拒绝复用()
        {
            var path = new List<Vector2Int>
            {
                Vector2Int.zero, Vector2Int.one
            };
            Assert.IsFalse(UnitMovementController.IsReusablePath(path,
                Vector2Int.zero, new Vector2Int(2, 2)));
        }

        [Test]
        public void 空路径或单格路径拒绝复用()
        {
            Assert.IsFalse(UnitMovementController.IsReusablePath(null,
                Vector2Int.zero, Vector2Int.one));
            Assert.IsFalse(UnitMovementController.IsReusablePath(
                new List<Vector2Int> { Vector2Int.zero }, Vector2Int.zero, Vector2Int.one));
        }
    }
}
#endif
