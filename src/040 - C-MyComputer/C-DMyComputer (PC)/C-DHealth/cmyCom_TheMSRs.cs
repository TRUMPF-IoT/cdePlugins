// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDMyComputer
{
    class TheMSRs
    {
        /* Intel defined MSRs. */
        public static uint MSR_IA32_P5_MC_ADDR = 0;
        public static uint MSR_IA32_P5_MC_TYPE = 1;
        public static uint MSR_IA32_PLATFORM_ID = 0x17;
        public static uint MSR_IA32_EBL_CR_POWERON = 0x2a;
        public static uint MSR_IA32_APICBASE = 0x1b;
        public static uint MSR_IA32_APICBASE_BSP = (1 << 8);
        public static uint MSR_IA32_APICBASE_ENABLE = (1 << 11);
        //public static uint MSR_IA32_APICBASE_BASE = (uint)(0xfffff << 12);
        public static uint MSR_IA32_UCODE_WRITE = 0x79;
        public static uint MSR_IA32_MPERF = 0xE7;
        public static uint MSR_IA32_APERF = 0xE8;
        public static uint MSR_IA32_UCODE_REV = 0x8b;
        public static uint MSR_IA32_BBL_CR_CTL = 0x119;
        public static uint MSR_IA32_MCG_CAP = 0x179;
        public static uint MSR_IA32_MCG_STATUS = 0x17a;
        public static uint MSR_IA32_MCG_CTL = 0x17b;
        public static uint MSR_IA32_THERM_CONTROL = 0x19a;
        public static uint MSR_IA32_THERM_INTERRUPT = 0x19b;
        public static uint MSR_IA32_THERM_STATUS = 0x19c;
        public static uint MSR_IA32_MISC_ENABLE = 0x1a0;
        public static uint MSR_IA32_DEBUGCTLMSR = 0x1d9;
        public static uint MSR_IA32_LASTBRANCHFROMIP = 0x1db;
        public static uint MSR_IA32_LASTBRANCHTOIP = 0x1dc;
        public static uint MSR_IA32_LASTINTFROMIP = 0x1dd;
        public static uint MSR_IA32_LASTINTTOIP = 0x1de;
        public static uint MSR_IA32_MC0_CTL = 0x400;
        public static uint MSR_IA32_MC0_STATUS = 0x401;
        public static uint MSR_IA32_MC0_ADDR = 0x402;
        public static uint MSR_IA32_MC0_MISC = 0x403;
        public static uint MSR_P6_PERFCTR0 = 0xc1;
        public static uint MSR_P6_PERFCTR1 = 0xc2;
        public static uint MSR_P6_EVNTSEL0 = 0x186;
        public static uint MSR_P6_EVNTSEL1 = 0x187;
        public static uint MSR_IA32_PERF_STATUS = 0x198;
        public static uint MSR_IA32_PERF_CTL = 0x199;

        /* AMD Defined MSRs */
        public static uint MSR_K6_EFER = 0xC0000080;
        public static uint MSR_K6_STAR = 0xC0000081;
        public static uint MSR_K6_WHCR = 0xC0000082;
        public static uint MSR_K6_UWCCR = 0xC0000085;
        public static uint MSR_K6_EPMR = 0xC0000086;
        public static uint MSR_K6_PSOR = 0xC0000087;
        public static uint MSR_K6_PFIR = 0xC0000088;
        public static uint MSR_K7_EVNTSEL0 = 0xC0010000;
        public static uint MSR_K7_PERFCTR0 = 0xC0010004;
        public static uint MSR_K7_HWCR = 0xC0010015;
        public static uint MSR_K7_CLK_CTL = 0xC001001b;
        public static uint MSR_K7_FID_VID_CTL = 0xC0010041;
        public static uint MSR_K7_VID_STATUS = 0xC0010042;

        /* Centaur-Hauls/IDT defined MSRs. */
        public static uint MSR_IDT_FCR1 = 0x107;
        public static uint MSR_IDT_FCR2 = 0x108;
        public static uint MSR_IDT_FCR3 = 0x109;
        public static uint MSR_IDT_FCR4 = 0x10a;
        public static uint MSR_IDT_MCR0 = 0x110;
        public static uint MSR_IDT_MCR1 = 0x111;
        public static uint MSR_IDT_MCR2 = 0x112;
        public static uint MSR_IDT_MCR3 = 0x113;
        public static uint MSR_IDT_MCR4 = 0x114;
        public static uint MSR_IDT_MCR5 = 0x115;
        public static uint MSR_IDT_MCR6 = 0x116;
        public static uint MSR_IDT_MCR7 = 0x117;
        public static uint MSR_IDT_MCR_CTRL = 0x120;

        /* VIA Cyrix defined MSRs*/
        public static uint MSR_VIA_FCR = 0x1107;
        public static uint MSR_VIA_LONGHAUL = 0x110a;
        public static uint MSR_VIA_BCR2 = 0x1147;

        /* Transmeta defined MSRs */
        public static uint MSR_TMTA_LONGRUN_CTRL = 0x80868010;
        public static uint MSR_TMTA_LONGRUN_FLAGS = 0x80868011;
        public static uint MSR_TMTA_LRTI_READOUT = 0x80868018;
        public static uint MSR_TMTA_LRTI_VOLT_MHZ = 0x8086801a;
    }
}
