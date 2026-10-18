/* SPDX-FileCopyrightText: 2026 Paul R. Decker
 * SPDX-License-Identifier: GPL-3.0-only */
ENTRY(Reset_Handler)
MEMORY {
    FLASH (rx) : ORIGIN = 0, LENGTH = 32K
    RAM (rwx) : ORIGIN = 0x20000000, LENGTH = 4K
}
__StackTop = ORIGIN(RAM) + LENGTH(RAM);
__StackLimit = __StackTop - 1024;
PHDRS { code PT_LOAD FLAGS(5); data PT_LOAD FLAGS(6); }
SECTIONS {
    .text : {
        KEEP(*(.vectors))
        *(.text*) *(.rodata*)
        . = ALIGN(4);
    } > FLASH :code
    .data : { . = ALIGN(4); __data_start = .; *(.data*) . = ALIGN(4); __data_end = .; } > RAM AT> FLASH :data
    __data_load = LOADADDR(.data);
    .bss (NOLOAD) : { . = ALIGN(4); __bss_start = .; *(.bss*) *(COMMON) . = ALIGN(4); __bss_end = .; } > RAM :data
    .constructors : { KEEP(*(.init_array*)) KEEP(*(.preinit_array*)) } > RAM :data
    /* No exception unwinding, including SDK assembly .fnstart metadata. */
    /DISCARD/ : { *(.ARM.exidx*) *(.ARM.extab*) *(.stack*) *(.heap*) *(.comment) *(.note*) }
}
ASSERT(SIZEOF(.constructors) == 0, "Startup has no dynamic global initialization")
ASSERT(SIZEOF(.data) + SIZEOF(.bss) <= 2048, "Static RAM exceeds 2 KiB budget")
ASSERT(LOADADDR(.data) + SIZEOF(.data) <= 24576, "Flash exceeds 24 KiB budget")
ASSERT(ADDR(.bss) + SIZEOF(.bss) <= __StackLimit, "RAM overlaps 1 KiB stack")
ASSERT(!DEFINED(malloc) && !DEFINED(_malloc_r) && !DEFINED(_sbrk), "Unexpected heap dependency")
