/**
 * Rectangle packer
 *
 * Copyright 2012 Ville Koskela. All rights reserved.
 * Ported to Unity by Da Viking Code.
 * Adapted for DynamicAtlas (single insert / free rect).
 *
 * You may redistribute, use and/or modify this source code freely
 * but this copyright statement must not be removed from the source files.
 */
using System.Collections.Generic;
using UnityEngine;

namespace DynamicAtlas.Packing
{
    public class RectanglePacker
    {
        private int _width;
        private int _height;
        private int _padding;
        private int _packedWidth;
        private int _packedHeight;

        private readonly List<IntegerRectangle> _insertedRectangles = new List<IntegerRectangle>();
        private readonly List<IntegerRectangle> _freeAreas = new List<IntegerRectangle>();
        private readonly List<IntegerRectangle> _tempFreeAreas = new List<IntegerRectangle>();
        private readonly IntegerRectangle _outsideRectangle;

        private static readonly List<IntegerRectangle> s_rectPool = new List<IntegerRectangle>();

        public int RectangleCount => _insertedRectangles.Count;

        public RectanglePacker(int width, int height, int padding = 0)
        {
            _outsideRectangle = new IntegerRectangle(width + 1, height + 1, 0, 0);
            Reset(width, height, padding);
        }

        public void Reset(int width, int height, int padding = 0)
        {
            while (_insertedRectangles.Count > 0)
                ReleaseRect(_insertedRectangles.Pop());

            while (_freeAreas.Count > 0)
                ReleaseRect(_freeAreas.Pop());

            _width = width;
            _height = height;
            _packedWidth = 0;
            _packedHeight = 0;
            _padding = padding;
            _freeAreas.Add(AllocateRect(0, 0, _width, _height));
        }

        public bool TryPack(int width, int height, out IntegerRectangle packed)
        {
            packed = null;
            int index = GetFreeAreaIndex(width, height);
            if (index < 0)
                return false;

            IntegerRectangle freeArea = _freeAreas[index];
            IntegerRectangle target = AllocateRect(freeArea.X, freeArea.Y, width, height);
            GenerateNewFreeAreas(target, _tempFreeAreas);

            while (_tempFreeAreas.Count > 0)
                _freeAreas.Add(_tempFreeAreas.Pop());

            _insertedRectangles.Add(target);

            if (target.Right > _packedWidth)
                _packedWidth = target.Right;
            if (target.Top > _packedHeight)
                _packedHeight = target.Top;

            packed = target;
            return true;
        }

        public void FreeRectangle(int x, int y, int width, int height)
        {
            for (int i = _insertedRectangles.Count - 1; i >= 0; i--)
            {
                IntegerRectangle inserted = _insertedRectangles[i];
                if (inserted.X == x && inserted.Y == y && inserted.Width == width && inserted.Height == height)
                {
                    _insertedRectangles.RemoveAt(i);
                    ReleaseRect(inserted);
                    break;
                }
            }

            _freeAreas.Add(AllocateRect(x, y, width, height));
        }

        /// <summary>
        /// 尝试从空闲列表精确收回矩形并重新标记为占用（脏缓存复活）。
        /// </summary>
        public bool TryReclaimRectangle(int x, int y, int width, int height)
        {
            for (int i = 0; i < _freeAreas.Count; i++)
            {
                IntegerRectangle free = _freeAreas[i];
                if (free.X != x || free.Y != y || free.Width != width || free.Height != height)
                    continue;

                _freeAreas.RemoveAt(i);
                ReleaseRect(free);

                IntegerRectangle target = AllocateRect(x, y, width, height);
                _insertedRectangles.Add(target);

                if (target.Right > _packedWidth)
                    _packedWidth = target.Right;
                if (target.Top > _packedHeight)
                    _packedHeight = target.Top;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Debug：导出当前空闲矩形列表（不修改装箱状态）。
        /// </summary>
        public void CopyFreeRectangles(List<RectInt> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < _freeAreas.Count; i++)
            {
                IntegerRectangle free = _freeAreas[i];
                results.Add(new RectInt(free.X, free.Y, free.Width, free.Height));
            }
        }

        private int GetFreeAreaIndex(int width, int height)
        {
            IntegerRectangle best = _outsideRectangle;
            int index = -1;
            int paddedWidth = width + _padding;
            int paddedHeight = height + _padding;

            for (int i = _freeAreas.Count - 1; i >= 0; i--)
            {
                IntegerRectangle free = _freeAreas[i];
                if (free.X < _packedWidth || free.Y < _packedHeight)
                {
                    if (free.X < best.X && paddedWidth <= free.Width && paddedHeight <= free.Height)
                    {
                        index = i;
                        if ((paddedWidth == free.Width && free.Width <= free.Height && free.Right < _width) ||
                            (paddedHeight == free.Height && free.Height <= free.Width))
                            break;
                        best = free;
                    }
                }
                else
                {
                    if (free.X < best.X && width <= free.Width && height <= free.Height)
                    {
                        index = i;
                        if ((width == free.Width && free.Width <= free.Height && free.Right < _width) ||
                            (height == free.Height && free.Height <= free.Width))
                            break;
                        best = free;
                    }
                }
            }

            return index;
        }

        private void GenerateNewFreeAreas(IntegerRectangle target, List<IntegerRectangle> results)
        {
            int x = target.X;
            int y = target.Y;
            int right = target.Right + 1 + _padding;
            int top = target.Top + 1 + _padding;

            IntegerRectangle targetWithPadding = null;
            if (_padding == 0)
                targetWithPadding = target;

            for (int i = _freeAreas.Count - 1; i >= 0; i--)
            {
                IntegerRectangle area = _freeAreas[i];
                if (!(x >= area.Right || right <= area.X || y >= area.Top || top <= area.Y))
                {
                    if (targetWithPadding == null)
                        targetWithPadding = AllocateRect(target.X, target.Y, target.Width + _padding, target.Height + _padding);

                    GenerateDividedAreas(targetWithPadding, area, results);
                    IntegerRectangle topOfStack = _freeAreas.Pop();
                    if (i < _freeAreas.Count)
                        _freeAreas[i] = topOfStack;
                }
            }

            if (targetWithPadding != null && targetWithPadding != target)
                ReleaseRect(targetWithPadding);

            FilterSelfSubAreas(results);
        }

        private void GenerateDividedAreas(IntegerRectangle divider, IntegerRectangle area, List<IntegerRectangle> results)
        {
            int count = 0;
            int rightDelta = area.Right - divider.Right;
            if (rightDelta > 0)
            {
                results.Add(AllocateRect(divider.Right, area.Y, rightDelta, area.Height));
                count++;
            }

            int leftDelta = divider.X - area.X;
            if (leftDelta > 0)
            {
                results.Add(AllocateRect(area.X, area.Y, leftDelta, area.Height));
                count++;
            }

            int bottomDelta = area.Top - divider.Top;
            if (bottomDelta > 0)
            {
                results.Add(AllocateRect(area.X, divider.Top, area.Width, bottomDelta));
                count++;
            }

            int topDelta = divider.Y - area.Y;
            if (topDelta > 0)
            {
                results.Add(AllocateRect(area.X, area.Y, area.Width, topDelta));
                count++;
            }

            if (count == 0 && (divider.Width < area.Width || divider.Height < area.Height))
                results.Add(area);
            else
                ReleaseRect(area);
        }

        private void FilterSelfSubAreas(List<IntegerRectangle> areas)
        {
            for (int i = areas.Count - 1; i >= 0; i--)
            {
                IntegerRectangle filtered = areas[i];
                for (int j = areas.Count - 1; j >= 0; j--)
                {
                    if (i == j)
                        continue;

                    IntegerRectangle area = areas[j];
                    if (filtered.X >= area.X && filtered.Y >= area.Y &&
                        filtered.Right <= area.Right && filtered.Top <= area.Top)
                    {
                        ReleaseRect(filtered);
                        IntegerRectangle topOfStack = areas.Pop();
                        if (i < areas.Count)
                            areas[i] = topOfStack;
                        break;
                    }
                }
            }
        }

        private static IntegerRectangle AllocateRect(int x, int y, int width, int height)
        {
            if (s_rectPool.Count > 0)
            {
                IntegerRectangle rect = s_rectPool.Pop();
                rect.Set(x, y, width, height);
                return rect;
            }

            return new IntegerRectangle(x, y, width, height);
        }

        private static void ReleaseRect(IntegerRectangle rectangle)
        {
            s_rectPool.Add(rectangle);
        }
    }

    internal static class ListExtension
    {
        public static T Pop<T>(this List<T> list)
        {
            int index = list.Count - 1;
            T item = list[index];
            list.RemoveAt(index);
            return item;
        }
    }
}
