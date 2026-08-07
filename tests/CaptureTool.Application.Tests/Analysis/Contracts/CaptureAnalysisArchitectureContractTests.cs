using System.Reflection;
using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Analysis.Processing;
using CaptureTool.Application.Abstractions.Analysis.Queries;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Security;
using CaptureTool.Application.Analysis.Processing;
using CaptureTool.Domain.Analysis;

#pragma warning disable IL2026 // Architecture tests intentionally inspect untrimmed test assemblies.
#pragma warning disable IL2070 // Architecture tests intentionally inspect untrimmed public contract metadata.

namespace CaptureTool.Application.Tests.Analysis.Contracts;

[TestClass]
public sealed class CaptureAnalysisArchitectureContractTests
{
    private const string AnalysisContractsNamespace =
        "CaptureTool.Application.Abstractions.Analysis";

    [TestMethod]
    public void AnalysisDomain_ShouldReferenceOnlyTheSharedDomainAmongCaptureToolProjects()
    {
        Assembly domainAssembly = typeof(CaptureAnalysisRecord).Assembly;
        string[] captureToolReferences = domainAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name != null && name.StartsWith("CaptureTool.", StringComparison.Ordinal))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "CaptureTool.Domain" },
            captureToolReferences,
            "The Analysis domain may reference only the shared-kernel Domain project.");
    }

    [TestMethod]
    public void AnalysisDomain_ShouldNotReferenceForbiddenFrameworkOrProviderAssemblies()
    {
        string[] forbiddenReferences = typeof(CaptureAnalysisRecord).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name != null && IsForbiddenAssemblyReference(name))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.IsEmpty(
            forbiddenReferences,
            $"The Analysis domain has forbidden assembly references: {string.Join(", ", forbiddenReferences)}");
    }

    [TestMethod]
    public void AnalysisContracts_ShouldExposeNoForbiddenTypesOrFilesystemPaths()
    {
        Type[] contractTypes = GetAnalysisContractTypes();
        var violations = new List<string>();

        foreach (Type contractType in contractTypes)
        {
            foreach ((string location, Type signatureType) in GetPublicSignatureTypes(contractType))
            {
                foreach (Type componentType in ExpandSignatureType(signatureType))
                {
                    string? reason = GetForbiddenTypeReason(componentType);
                    if (reason != null)
                    {
                        violations.Add($"{location} exposes {componentType}: {reason}");
                    }
                }
            }

            violations.AddRange(GetPathShapedMembers(contractType));
        }

        string[] distinctViolations = violations
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.IsEmpty(
            distinctViolations,
            "Forbidden Analysis contract surface:" + Environment.NewLine +
            string.Join(Environment.NewLine, distinctViolations));
    }

    [TestMethod]
    public void CapabilityPayload_ShouldBeAClosedCompiledContractFamily()
    {
        Type payloadType = typeof(CapabilityPayload);

        Assert.IsTrue(payloadType.IsClass);
        Assert.IsTrue(payloadType.IsAbstract);

        ConstructorInfo[] externallyAccessibleConstructors = payloadType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(constructor =>
                constructor.IsPublic ||
                constructor.IsFamily ||
                constructor.IsFamilyOrAssembly)
            .ToArray();

        Assert.IsEmpty(
            externallyAccessibleConstructors,
            "CapabilityPayload must not be subclassable outside the Analysis domain assembly.");

        Type[] payloadInterfaces = GetAnalysisContractTypes()
            .Where(type =>
                type.IsInterface &&
                type.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.IsEmpty(
            payloadInterfaces,
            $"Payload interfaces are publicly extensible: {string.Join(", ", payloadInterfaces.Select(type => type.FullName))}");

        Type[] unsealedPayloads = payloadType.Assembly
            .GetExportedTypes()
            .Where(type => type.IsSubclassOf(payloadType) && !type.IsAbstract && !type.IsSealed)
            .ToArray();

        Assert.IsEmpty(
            unsealedPayloads,
            $"Compiled payload implementations must be sealed: {string.Join(", ", unsealedPayloads.Select(type => type.FullName))}");
    }

    [TestMethod]
    public void RequiredAnalysisPorts_ShouldExistAsPublicInterfaces()
    {
        Type[] requiredPorts =
        [
            typeof(ICaptureAssetChangeReader),
            typeof(ICaptureAnalysisWakeSignal),
            typeof(ICaptureAnalysisWakeWaiter),
            typeof(ICaptureAnalyzer),
            typeof(ICaptureAnalyzerCatalog),
            typeof(ICaptureAnalyzerResolver),
            typeof(ICaptureAnalysisSourceVerifier),
            typeof(ICaptureAnalysisScheduler),
            typeof(ICaptureAnalysisStore),
            typeof(ICaptureAnalysisMutationCoordinator),
            typeof(ICaptureAnalysisControlStore),
            typeof(ICaptureAnalysisJobStore),
            typeof(ICaptureAnalysisPolicyService),
            typeof(ICaptureAnalysisFeatureAvailability),
            typeof(IAnalysisCapabilityPreparationQueryService),
            typeof(ICaptureAnalysisWorker),
            typeof(ICaptureAnalysisQueryService),
            typeof(ICaptureMemorySearchService),
        ];

        foreach (Type port in requiredPorts)
        {
            Assert.IsTrue(port.IsPublic, $"{port.FullName} must be public.");
            Assert.IsTrue(port.IsInterface, $"{port.FullName} must be an interface.");
            StringAssert.StartsWith(
                port.Namespace,
                AnalysisContractsNamespace,
                $"{port.FullName} must live under the Analysis application-contract namespace.");
        }
    }

    [TestMethod]
    public void CurrentUserDataProtectionPort_ShouldBeReusedFromSecurityNamespace()
    {
        Assembly contractsAssembly = typeof(ICaptureAnalyzer).Assembly;
        Type[] dataProtectionPorts = contractsAssembly
            .GetExportedTypes()
            .Where(type => type.Name == nameof(IUserDataProtectionService))
            .ToArray();

        Assert.HasCount(1, dataProtectionPorts);
        Assert.AreSame(typeof(IUserDataProtectionService), dataProtectionPorts[0]);
        Assert.AreEqual(
            "CaptureTool.Application.Abstractions.Security",
            dataProtectionPorts[0].Namespace);
        Assert.IsFalse(
            GetAnalysisContractTypes().Any(type => type.Name == nameof(IUserDataProtectionService)),
            "Analysis must reuse the cross-cutting Security port instead of declaring another one.");
    }

    [TestMethod]
    public void MetadataWrites_ShouldRequireTheConditionalMutationCoordinator()
    {
        string[] storeMethods = typeof(ICaptureAnalysisStore)
            .GetMethods()
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "GetAsync", "ReadAllAsync" }, storeMethods);

        MethodInfo[] mutationMethods = typeof(ICaptureAnalysisMutationCoordinator).GetMethods();
        Assert.HasCount(4, mutationMethods);
        Assert.IsFalse(mutationMethods
            .SelectMany(method => method.GetParameters())
            .Any(parameter => parameter.Name == "currentPreconditions"));
    }

    [TestMethod]
    public void BackgroundWorker_ShouldNeverDependOnUserInitiatedPreparationCommands()
    {
        Type[] constructorDependencies = typeof(CaptureAnalysisWorker)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        CollectionAssert.DoesNotContain(
            constructorDependencies,
            typeof(IUserInitiatedAnalysisCapabilityPreparationService));
    }

    private static Type[] GetAnalysisContractTypes()
    {
        Assembly contractsAssembly = typeof(ICaptureAnalyzer).Assembly;
        Assembly domainAssembly = typeof(CaptureAnalysisRecord).Assembly;

        return contractsAssembly
            .GetExportedTypes()
            .Where(type =>
                type.Namespace?.StartsWith(AnalysisContractsNamespace, StringComparison.Ordinal) == true)
            .Concat(domainAssembly.GetExportedTypes().Where(type =>
                type.Namespace?.StartsWith("CaptureTool.Domain.Analysis", StringComparison.Ordinal) == true))
            .Distinct()
            .ToArray();
    }

    private static IEnumerable<(string Location, Type SignatureType)> GetPublicSignatureTypes(
        Type contractType)
    {
        const BindingFlags PublicDeclared =
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        if (contractType.BaseType != null && contractType.BaseType != typeof(object))
        {
            yield return ($"{contractType.FullName} base type", contractType.BaseType);
        }

        foreach (Type implementedInterface in contractType.GetInterfaces())
        {
            yield return ($"{contractType.FullName} interface", implementedInterface);
        }

        foreach (Type genericArgument in contractType.GetGenericArguments())
        {
            foreach (Type constraint in genericArgument.GetGenericParameterConstraints())
            {
                yield return ($"{contractType.FullName} generic constraint", constraint);
            }
        }

        foreach (ConstructorInfo constructor in contractType.GetConstructors(PublicDeclared))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                yield return ($"{contractType.FullName} constructor parameter '{parameter.Name}'", parameter.ParameterType);
            }
        }

        foreach (PropertyInfo property in contractType.GetProperties(PublicDeclared))
        {
            yield return ($"{contractType.FullName}.{property.Name}", property.PropertyType);

            foreach (ParameterInfo parameter in property.GetIndexParameters())
            {
                yield return ($"{contractType.FullName}.{property.Name} index parameter '{parameter.Name}'", parameter.ParameterType);
            }
        }

        foreach (FieldInfo field in contractType.GetFields(PublicDeclared))
        {
            yield return ($"{contractType.FullName}.{field.Name}", field.FieldType);
        }

        foreach (EventInfo eventInfo in contractType.GetEvents(PublicDeclared))
        {
            if (eventInfo.EventHandlerType != null)
            {
                yield return ($"{contractType.FullName}.{eventInfo.Name}", eventInfo.EventHandlerType);
            }
        }

        foreach (MethodInfo method in contractType.GetMethods(PublicDeclared))
        {
            if (method.IsSpecialName || method.GetBaseDefinition().DeclaringType == typeof(object))
            {
                continue;
            }

            if (method.ReturnType != typeof(void))
            {
                yield return ($"{contractType.FullName}.{method.Name} return", method.ReturnType);
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                yield return ($"{contractType.FullName}.{method.Name} parameter '{parameter.Name}'", parameter.ParameterType);
            }

            foreach (Type genericArgument in method.GetGenericArguments())
            {
                foreach (Type constraint in genericArgument.GetGenericParameterConstraints())
                {
                    yield return ($"{contractType.FullName}.{method.Name} generic constraint", constraint);
                }
            }
        }
    }

    private static IEnumerable<Type> ExpandSignatureType(Type type)
    {
        yield return type;

        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (Type component in ExpandSignatureType(elementType))
            {
                yield return component;
            }
        }

        foreach (Type genericArgument in type.GetGenericArguments())
        {
            foreach (Type component in ExpandSignatureType(genericArgument))
            {
                yield return component;
            }
        }
    }

    private static string? GetForbiddenTypeReason(Type type)
    {
        if (type == typeof(object))
        {
            return "open-ended object payloads are not AOT-safe compiled contracts";
        }

        string typeNamespace = type.Namespace ?? string.Empty;
        string assemblyName = type.Assembly.GetName().Name ?? string.Empty;

        if (typeNamespace.StartsWith("System.Text.Json", StringComparison.Ordinal) ||
            assemblyName.Equals("System.Text.Json", StringComparison.Ordinal))
        {
            return "JSON belongs in persistence infrastructure";
        }

        if (typeNamespace.StartsWith("System.Drawing", StringComparison.Ordinal) ||
            assemblyName.StartsWith("System.Drawing", StringComparison.Ordinal))
        {
            return "Analysis contracts use their own media geometry types";
        }

        if (IsWindowsOrProviderNamespace(typeNamespace) || IsWindowsOrProviderAssembly(assemblyName))
        {
            return "provider and WinRT types belong in provider infrastructure";
        }

        if (typeNamespace.StartsWith("CaptureTool.Presentation", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("CaptureTool.Infrastructure", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("CaptureTool.FeatureManagement", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Microsoft.Extensions.FeatureManagement", StringComparison.Ordinal))
        {
            return "presentation, infrastructure, and feature-management types are outside the contract boundary";
        }

        if (IsFilesystemType(type))
        {
            return "filesystem location types and file handles must not cross the Analysis contract boundary";
        }

        return null;
    }

    private static IEnumerable<string> GetPathShapedMembers(Type contractType)
    {
        const BindingFlags PublicDeclared =
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        foreach (PropertyInfo property in contractType.GetProperties(PublicDeclared))
        {
            if (IsPathName(property.Name))
            {
                yield return $"{contractType.FullName}.{property.Name} exposes a filesystem path-shaped property";
            }
        }

        foreach (FieldInfo field in contractType.GetFields(PublicDeclared))
        {
            if (IsPathName(field.Name))
            {
                yield return $"{contractType.FullName}.{field.Name} exposes a filesystem path-shaped field";
            }
        }

        foreach (MethodBase method in contractType
                     .GetMethods(PublicDeclared)
                     .Cast<MethodBase>()
                     .Concat(contractType.GetConstructors(PublicDeclared)))
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (IsPathName(parameter.Name))
                {
                    yield return $"{contractType.FullName}.{method.Name} parameter '{parameter.Name}' exposes a filesystem path";
                }
            }
        }
    }

    private static bool IsPathName(string? name)
    {
        return name != null &&
            (name.Equals("path", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith("Path", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith("Paths", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFilesystemType(Type type)
    {
        if (!string.Equals(type.Namespace, "System.IO", StringComparison.Ordinal))
        {
            return false;
        }

        return type.Name is
            "File" or
            "FileInfo" or
            "FileStream" or
            "FileSystemInfo" or
            "FileSystemWatcher" or
            "FileMode" or
            "FileAccess" or
            "FileShare" or
            "Directory" or
            "DirectoryInfo" or
            "DriveInfo" or
            "Path";
    }

    private static bool IsForbiddenAssemblyReference(string assemblyName)
    {
        return assemblyName is
                "CaptureTool.Domain.Capture" or
                "CaptureTool.Domain.Edit" or
                "System.Text.Json" or
                "System.Drawing" or
                "System.Drawing.Common" ||
            assemblyName.StartsWith("System.IO.FileSystem", StringComparison.Ordinal) ||
            assemblyName.StartsWith("CaptureTool.Presentation", StringComparison.Ordinal) ||
            assemblyName.StartsWith("CaptureTool.Infrastructure", StringComparison.Ordinal) ||
            assemblyName.StartsWith("CaptureTool.FeatureManagement", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal) ||
            IsWindowsOrProviderAssembly(assemblyName);
    }

    private static bool IsWindowsOrProviderNamespace(string typeNamespace)
    {
        return typeNamespace.Equals("Windows", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Windows.", StringComparison.Ordinal) ||
            typeNamespace.Equals("WinRT", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("WinRT.", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Microsoft.Windows", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Microsoft.UI.Xaml", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Microsoft.Azure", StringComparison.Ordinal) ||
            typeNamespace.Equals("Azure", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Azure.", StringComparison.Ordinal) ||
            typeNamespace.Equals("OpenAI", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("OpenAI.", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Microsoft.ML.OnnxRuntime", StringComparison.Ordinal) ||
            typeNamespace.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal);
    }

    private static bool IsWindowsOrProviderAssembly(string assemblyName)
    {
        return assemblyName.Equals("WinRT.Runtime", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft.Windows", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft.UI.Xaml", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft.Azure", StringComparison.Ordinal) ||
            assemblyName.Equals("Azure.Core", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Azure.AI", StringComparison.Ordinal) ||
            assemblyName.Equals("OpenAI", StringComparison.Ordinal) ||
            assemblyName.StartsWith("OpenAI.", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft.ML.OnnxRuntime", StringComparison.Ordinal) ||
            assemblyName.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal);
    }
}

#pragma warning restore IL2070
#pragma warning restore IL2026
