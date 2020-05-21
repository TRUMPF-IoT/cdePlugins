# Plugin Source Code

You can load all the plugins in Visual Studio 2019 with the included [SLN file](CDEPlugins.sln).

You can download CDEX deloyment packages (ZIP files) with compiled binaries for each of the plugins from the repostory's Releases.

## Available plug-ins

- [CDMyComputer](./040%20-%20C-MyComputer/readme.md): makes KPIs about the host OS and PC/VM available as Thing properties and provides corresponding NMI.
- [CDMyVThings](066%20-%20C-MyVThings/readme.md): Virtual Things that let you generate data for testing or demonstration purposes.
- [CDMyVisitorLog](103%20-%20CMyVisitorLog/readme.md): A monitoring plug-in primarily for use on Cloud Relays. It allows you to classify the connected Relays based on the plug-ins that are installed on them, and exposes them as thing properties/KPIs.
- [CDMyThingProvisioning](175%20-%20ThingProvisioning/readme.md): Facilities the automatic configuration of headless relay node. A simple scripting format (.CDEScript) sends parameterized messages (TSMs) to create plug-in instances or configure those plug-ins.
- [CDMyLogger](178%20-%20External%20Logger/readme.md): The logger can connect the internal SystemLog of the C-DEngine to external loggers such as SysGen or Greylog.
- [CDMyMeshSender](179%20-%20Mesh%20Sender\readme.md): The Mesh Sender allows to send specific telegrams to other nodes using reliable transport and queueing mechanisms.
- [CDMyMeshReceiver](180%20-%20Mesh%20Receiver\readme.md): The Mesh Receiver is the counterpart to the Mesh Sender. It receives the messages and sends back the acknowledgement telegrams required for reliable messaging.
- [CDMyPrometheusExporter](188%20-%20PrometheusExporter\readme.md): The prometheus exporter exports defined KPIs and makes them available via a prometheus scraper URL

Additional plug-ins like OPC UA Client and Server, ModBus, MTConnect, Azure Event/IoTHub, Google IoTHub, SmartThings, Wemo, Alexa, OpenAuth2 are under review for future OSS release. Let us know if you have a particular interest or requirement. Feel free to open an Issue/Feature Request on GitHub, or contact us at [info@c-labs.com](mailto:info@c-labs.com).
