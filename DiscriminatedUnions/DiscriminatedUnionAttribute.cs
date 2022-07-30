using System;

namespace nemuikoneko.DiscriminatedUnions;

public class UnionAttribute : Attribute
{
    public bool AllowDefault { get; set; }
}
