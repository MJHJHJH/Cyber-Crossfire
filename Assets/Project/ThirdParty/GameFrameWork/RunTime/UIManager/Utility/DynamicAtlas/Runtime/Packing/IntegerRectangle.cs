/**
 * Integer rectangle
 *
 * Copyright 2012 Ville Koskela. All rights reserved.
 * Ported to Unity by Da Viking Code.
 * Adapted for DynamicAtlas.
 *
 * You may redistribute, use and/or modify this source code freely
 * but this copyright statement must not be removed from the source files.
 */
namespace DynamicAtlas.Packing
{
    public class IntegerRectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int Right;
        public int Top;
        public int Id;

        public IntegerRectangle(int x = 0, int y = 0, int width = 0, int height = 0)
        {
            Set(x, y, width, height);
        }

        public void Set(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Right = x + width;
            Top = y + height;
        }
    }
}
