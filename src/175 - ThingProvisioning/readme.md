<!--
SPDX-FileCopyrightText: 2013-2020 TRUMPF Laser GmbH, authors: C-Labs

SPDX-License-Identifier: MPL-2.0
-->

# Thing Provisioner

Facilities the automatic configuration of headless relay node. A simple scripting format (.CDEScript) sends parameterized messages (TSMs) to create Things and plug-in instances and  configure them.

Example .cdescripts are available in this depot.

## Change log

- 4.109.4:
  1. If no Target is specified, look for a top-level EngineName value in the TheScript.Parameters JSON object.
  2. Populate all scripts and steps on startup, before running any scripts. Show scripts that could not be loaded, including parse error messages.
  3. Update script status more reliably and capture step names and error messages
  4. Consider step in error if there is an output mapping with a variable name that contains "Error" (case sensitive)

