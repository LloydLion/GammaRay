using System.Reflection;

namespace GammaRay.Core.Monitoring;

public readonly record struct SystemReportPropertyDeclaration(string Name, Type ValueType, PropertyInfo PropertyInfo);
