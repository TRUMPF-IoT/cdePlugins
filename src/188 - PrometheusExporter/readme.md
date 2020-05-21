<!--
SPDX-FileCopyrightText: 2013-2020 TRUMPF Laser GmbH, authors: C-Labs

SPDX-License-Identifier: MPL-2.0
-->

# The Prometheus Exporter

The prometheus exporter exports defined KPIs and makes them available via a prometheus scraper URL

The cde_kpi* KPIs are intended for use by plug-ins, but we are not using them for the CG plug-ins as with Prometheus we have a more flexible way to expose KPIs by simply declaring that a thing/property should be exposed as a prometheus metric.
Per standard prometheus convention there are gauges (current values) and counters (_total) flavors for most KPIs.
