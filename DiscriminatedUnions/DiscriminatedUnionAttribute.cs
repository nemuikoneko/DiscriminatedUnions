using System;

namespace nemuikoneko.DiscriminatedUnions;

public class DiscriminatedUnionAttribute : Attribute
{
    public bool AllowDefault { get; set; }
}
