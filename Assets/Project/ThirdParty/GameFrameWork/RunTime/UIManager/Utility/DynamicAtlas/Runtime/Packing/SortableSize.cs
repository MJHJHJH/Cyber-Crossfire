/**
 * Sortable size
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
    public class SortableSize
    {
        public int Width;
        public int Height;
        public int Id;

        public SortableSize(int width, int height, int id)
        {
            Width = width;
            Height = height;
            Id = id;
        }
    }
}
