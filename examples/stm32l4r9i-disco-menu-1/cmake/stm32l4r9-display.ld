/* Application memory contract for STM32L4R9AII6 / MB1314.
 * The compressed RGB888 framebuffer has its own SRAM3 region. Startup deliberately
 * does not zero this NOLOAD section; the display adapter initializes it before use.
 */
ENTRY(Reset_Handler)
MEMORY
{
    FLASH (rx)  : ORIGIN = 0x08000000, LENGTH = 2048K
    RAM (xrw)   : ORIGIN = 0x20000000, LENGTH = 192K
    RAM2 (xrw)  : ORIGIN = 0x10000000, LENGTH = 64K
    RAM3 (xrw)  : ORIGIN = 0x20040000, LENGTH = 384K
}
_estack = ORIGIN(RAM) + LENGTH(RAM);
_Min_Stack_Size = 8K;
__stack_limit__ = _estack - _Min_Stack_Size;
SECTIONS
{
    .isr_vector : { . = ALIGN(8); KEEP(*(.isr_vector)) . = ALIGN(8); } > FLASH
    .text :
    {
        *(.text .text.*) *(.glue_7 .glue_7t) KEEP(*(.init)) KEEP(*(.fini))
        . = ALIGN(8); _etext = .;
    } > FLASH
    .rodata : { *(.rodata .rodata.*) . = ALIGN(8); } > FLASH
    .ARM.extab : { *(.ARM.extab* .gnu.linkonce.armextab.*) } > FLASH
    .ARM.exidx :
    {
        __exidx_start = .; *(.ARM.exidx*) __exidx_end = .;
    } > FLASH
    .preinit_array (READONLY) :
    {
        PROVIDE_HIDDEN(__preinit_array_start = .);
        KEEP(*(.preinit_array*)) PROVIDE_HIDDEN(__preinit_array_end = .);
    } > FLASH
    .init_array (READONLY) :
    {
        PROVIDE_HIDDEN(__init_array_start = .);
        KEEP(*(SORT(.init_array.*))) KEEP(*(.init_array*))
        PROVIDE_HIDDEN(__init_array_end = .);
    } > FLASH
    .fini_array (READONLY) :
    {
        PROVIDE_HIDDEN(__fini_array_start = .);
        KEEP(*(SORT(.fini_array.*))) KEEP(*(.fini_array*))
        PROVIDE_HIDDEN(__fini_array_end = .);
    } > FLASH
    _sidata = LOADADDR(.data);
    .data :
    {
        . = ALIGN(8); _sdata = .;
        *(.data .data.*) *(.RamFunc .RamFunc.*)
        . = ALIGN(8); _edata = .;
    } > RAM AT > FLASH
    .bss (NOLOAD) :
    {
        . = ALIGN(8); _sbss = .; __bss_start__ = .;
        *(.bss .bss.*) *(COMMON)
        . = ALIGN(8); _ebss = .; __bss_end__ = .;
    } > RAM
    .noinit (NOLOAD) : { *(.noinit .noinit.*) . = ALIGN(8); } > RAM
    _end = .; PROVIDE(end = .);
    .stack __stack_limit__ (NOLOAD) :
    {
        __stack_bottom__ = .; . += _Min_Stack_Size; __stack_top__ = .;
    } > RAM
    .display_framebuffer (NOLOAD) :
    {
        . = ALIGN(16); __display_framebuffer_start__ = .;
        KEEP(*(.display_framebuffer .display_framebuffer.*))
        . = ALIGN(16); __display_framebuffer_end__ = .;
    } > RAM3
    .ARM.attributes 0 : { *(.ARM.attributes) }
    /DISCARD/ : { *(.note.GNU-stack) }
}
ASSERT(_end <= __stack_limit__, "SRAM1 data overlaps the reserved 8 KiB stack")
ASSERT(__stack_top__ == _estack, "Unexpected stack placement")
ASSERT(__display_framebuffer_start__ == ORIGIN(RAM3), "Framebuffer must begin at SRAM3")
ASSERT(SIZEOF(.display_framebuffer) == 365040, "Expected the 365040-byte RGB888 GFXMMU framebuffer")
ASSERT(__display_framebuffer_end__ <= ORIGIN(RAM3) + LENGTH(RAM3), "Framebuffer exceeds SRAM3")
ASSERT((__display_framebuffer_start__ & 15) == 0, "Framebuffer must be 16-byte aligned")
