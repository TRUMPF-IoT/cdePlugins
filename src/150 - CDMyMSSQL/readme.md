# MS SQL Server Plugin

This plugin allows to store data collected via the C-DEngine in a Microsoft SQL Server.
In order to configure the plugin, three parameters have to be added to the App.Config:

```
                SQLServerName=<your Server Name or IP>
                SQLUserName=<the SQL Server username>
                SQLPassword=<the sql server password>
```

When the node boots for the first time, these settings will be removed from the active app.config and stored encrypted in the settings store of the C-DEngine.
Any changes to these settings after the first boot will no longer be used by the node.
