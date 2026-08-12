#:project ../GammaRay.Core/GammaRay.Core.csproj
#:package BenchmarkDotNet@0.15.8
#:property PublishAot=false
#:property Configuration=Release
#:property Optimize=true

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using GammaRay.Core.Settings.Binding;
using GammaRay.Core.Settings.Model;
using GammaRay.Core.Settings.Tree.Loading;
using GammaRay.Core.Utils.FileSystem;

BenchmarkRunner.Run<MyBenchmark>();

[MemoryDiagnoser]
[InProcess]
public class MyBenchmark
{
	[Benchmark]
	public object CreateConfigurationModel()
	{
		var treeLoader = new YAMLSettingsTreeLoader();
		var tree = treeLoader.LoadTree(ConfigFile.Config.Replace("\t", "    "));
		var binder = SettingsTreeAggregateBinderSource.Create(NullFileSystemLocator.Instance);
		var model = binder.Bind<SettingsModelRoot>(tree.Root);
		return model;
	}
}

public static class ConfigFile
{
	public const string Config =
	"""
	api:
	  endPoints:
		- bindAddress: 127.0.0.3
		  port: 5000

	inbounds:
	  hostHttpIn: http://127.0.0.3:2000
	  hostSocksIn:
		protocol: socks
		endPoint: 127.0.0.3:2001
	  localNetHttpIn: http://0.0.0.0:2002

	internetAccessPoints:
	  finlandProxy:
		channels:
		  ms.gl-n.ru:
			protocol: socks
			endPoint: 127.0.0.3:20020
			tags: []
			availableInNetwork: [ default ]
		  direct-1:
			protocol: socks
			endPoint: 127.0.0.3:20021
			tags: []
			availableInNetwork: [ default ]
		  direct-2:
			protocol: socks
			endPoint: 127.0.0.3:20022
			tags: []
			availableInNetwork: [ default ]
		  direct-3:
			protocol: socks
			endPoint: 127.0.0.3:20023
			tags: []
			availableInNetwork: [ default ]
		  direct-4:
			protocol: socks
			endPoint: 127.0.0.3:20024
			tags: []
			availableInNetwork: [ default ]
		  direct-5:
			protocol: socks
			endPoint: 127.0.0.3:20025
			tags: []
			availableInNetwork: [ default ]
		  direct-6:
			protocol: socks
			endPoint: 127.0.0.3:20026
			tags: []
			availableInNetwork: [ default ]
		  direct-7:
			protocol: socks
			endPoint: 127.0.0.3:20027
			tags: []
			availableInNetwork: [ default ]


	capabilityClasses:
	  HTTPS:
		detectionRules:
		  - transport: StreamBased
			port: 443
		probingMethod:
		  driver: HTTP
		  parameters:
			useTLS: true
			path: ""
			method: GET
	  HTTP:
		detectionRules:
		  - transport: StreamBased
			port: 80
		probingMethod:
		  driver: HTTP
		  parameters:
			useTLS: false
			path: ""
			method: GET

	endpointRoutingConfigurations:
	  default:
		IAPChain: [ [local:*, finlandProxy] ]
		defaultIAPChain: [ finlandProxy, local:* ]

	  ruDomains:
		IAPChain: [ [local:*], [finlandProxy] ]
		defaultIAPChain: [ local:* ]

	  viaProxy:
		IAPChain: [ [finlandProxy] ]
		queueMode: restricted

	  viaLocal:
		IAPChain: [ [local:*] ]
		queueMode: restricted

	networkProfiles: {}

	endPointCategories:
	  ruDomains: { patterns: [ ru ] }
	  blockedInRussia: { patterns: [ 2ip.ru, chatgpt.com ] }
	  blockedInNonRussia: { patterns: [ yandex.ru ] }

	routingRules:
	  - endPointCategory: blockedInRussia
		networkProfile: default  
		to: viaProxy

	  - endPointCategory: blockedInNonRussia
		to: viaLocal

	  - endPointCategory: ruDomains
		to: ruDomains

	  - to: default

	""";
}
