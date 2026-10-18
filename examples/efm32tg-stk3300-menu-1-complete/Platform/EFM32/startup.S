/* SPDX-FileCopyrightText: 2026 Paul R. Decker
 * SPDX-License-Identifier: GPL-3.0-only */
.syntax unified
.cpu cortex-m3
.thumb
.section .vectors,"a",%progbits
.balign 256
.global __Vectors
__Vectors:
.word __StackTop, Reset_Handler
.word Fault_Handler, Fault_Handler, Fault_Handler, Fault_Handler, Fault_Handler
.word 0, 0, 0, 0
.word Fault_Handler, Fault_Handler, 0, Fault_Handler, SysTick_Handler
.rept 23
.word Fault_Handler
.endr

.section .text.Reset_Handler,"ax",%progbits
.thumb_func
.global Reset_Handler
Reset_Handler:
    ldr r0, =__data_load
    ldr r1, =__data_start
    ldr r2, =__data_end
1:  cmp r1, r2
    bhs 2f
    ldr r3, [r0], #4
    str r3, [r1], #4
    b 1b
2:  ldr r1, =__bss_start
    ldr r2, =__bss_end
    movs r3, #0
3:  cmp r1, r2
    bhs 4f
    str r3, [r1], #4
    b 3b
4:  ldr r1, =__StackLimit
    ldr r2, =__StackTop
    ldr r3, =0xA5A5A5A5
6:  cmp r1, r2
    bhs 7f
    str r3, [r1], #4
    b 6b
7:  bl SystemInit
    bl main
    b Fault_Handler
.thumb_func
.global Fault_Handler
Fault_Handler:
    cpsid i
5:  wfi
    b 5b
