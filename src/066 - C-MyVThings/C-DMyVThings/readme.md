<!--
SPDX-FileCopyrightText: TRUMPF Laser GmbH, authors: C-Labs
SPDX-License-Identifier: MPL-2.0
-->

# Virtual Things Plug-in

## Virtual Sensor
Consumes a single property from a selectable thing into the VSensor thing (Value property), and represents it as a sensor using the default sensor template (chart, units, moving min/max/avg etc.)

Default Sensor Template:
- Uses historian to collect values of a single property info a storage mirror
- Property recorded as "QValue", mapped from a selectable property on the seme thing. Property Name selected via "StateSensorValue" property.
- Computes moving average, minimum and maximum of the QValue (into QValue_Ave, QValue_Min, QValue_Max properties) and records them into the storage mirror.

## Virtual State Sensor
Consumes a single property from a selectable thing and generates a single numerical state (typically on/off) from it's value through configurable mechanisms:
1) Regular Expression:
	- filter regex: extracts value of the first match from an input string/value
	- value regex: 1 if match, 0 if no match
2) Choice of min, max or avg for a configurable time interval (restarts every n milli seconds)

Internally, it maps the external property to a "RawValue" property in the virtual sensor thing itself (using TheThingRegistry.PropertyMapper), which it then processes into the "Value" property of the VStateSender thing.

## Data Player

This device type of the VThings plug-in lets you play thing updates from a log file (i.e. a mesh sender data log file) into one or more things.
It's primary purpose is to test plug-ins with particular update sequences to help reproduce problems that only occur at customer sites.

Basic operation:
1) Drag & drop a log file onto the uploader, or place a file called "meshsenderdata.log" into the relay directory

- An easy option is to turn on the "Log Sent Payload Data option" in the Mesh Sender plug-in (pick the thing you want to record, chose the JSON Thing event format, configure the other options). Optionally turn on the "Do not wait for ACKs" option if you want to purely record updates for a particular thing and don't have a mesh receiver in your mesh/on your node.

2) Tap start: the plug-in will read the file contents and play the updates into the same thing (engine name/devicetype, ID and cdeMID) that generated them. Like the Mesh Receiver, it finds the thing by ID, not by cdeMID. It creates the thing if it does not exist, and obeys the timing of the original property updates.
3) When the run is finished, the plug-in reports the number of property updates applied and basic throughput information (props/s)

Options:
- Speed factor: speeds up or slows down the rate at which updates are applied.
   - 1 (default): same speed as original updates
   - greater than 1: faster than original, less than 1: slower than original
   - 0: ignore original timing and play as fast as possible (or throttled as configured)
- Maximum Delay (in ms): limits the wait time between updates (only used with speed factor greater than 0). Useful when a machine sits idle for minutes or hours, but you want to get the update sequence of the day (or week). 0 = use original timing (default)
- Item Delay (in ms): used only for speed factor == 0. Pauses the indicated time between each update. Use delay of 0 with caution.
- Adjust times: if checked, changes the timestamps (cdeP.cdeCTIM) to the current system time when the update is played back. Unchecked means that the originally recorded timestamps are used.
- Restart: restarts the playback when all data has been played back
- Input file: name of the file from which to read the thing update information. Must be under the ClientBin folder (per usual CDEngine rules), and usually drag&dropped there using the file dropper control. Exception: if the name is meshsenderdata.log (default), the file is read from the relay directory (where the mesh sender puts it's data log files).
- Number of Things: default 1. Lets you play the updates multiple times, on parallel tasks/threads. Useful for stress testing with a single thing (somewhat), but more so with the Engine Name/Device Type option to actually create additional thing instances.
- Engine Name/Device Type: when specified, the player will create new thing instances using an ID and FriendlyName of "Playback000000" (incrementing when number of things is greater than 1).
   - Try "CDMyVThings.TheVThings" and "Memory Tag"
   - For Device Gate use "CDMyMachineConnector.MachineConnectorService", "Machine Connector".

## Countdown

## Sine Wave generator

## Memory Tag

## NMI Element

## Data Generator

## Data Verifier

