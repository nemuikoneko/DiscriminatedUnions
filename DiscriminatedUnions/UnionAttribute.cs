using System;

namespace nemuikoneko.DiscriminatedUnions;

public sealed class UnionAttribute : Attribute
{
    public bool AllowDefault { get; set; }
}
