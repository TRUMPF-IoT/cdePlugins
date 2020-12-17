
##Change Log:
- 4.109.4: 
  1. Rename OPC UA library to cdeOPC* to avoid collisions with plug-ins using the OPC SDK (cdeOPC is forked)
  2. Add support for creating tags with thing references by EngineName/DeviceType/FriendlyName etc.
  3. Allow tags in an create tag message to mention the host thing only on the first tag
