using System;

namespace CaptureTool.Common.Loading;

public interface IHasParameterType
{
    Type ParameterType { get; }
}