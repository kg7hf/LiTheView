/* Bare-metal newlib boundary. This application has no filesystem, console or
 * dynamic heap. Unsupported calls fail explicitly; termination stops the CPU.
 * void pointers here are the required newlib C ABI, not application ownership.
 */
#include "stm32l4xx_hal.h"
#include <errno.h>
#include <stddef.h>
#include <sys/stat.h>
#include <sys/types.h>

int _close(int descriptor)
{
    (void)descriptor;
    errno = EBADF;
    return -1;
}
int _fstat(int descriptor, struct stat *status)
{
    (void)descriptor;
    (void)status;
    errno = EBADF;
    return -1;
}
int _isatty(int descriptor)
{
    (void)descriptor;
    errno = ENOTTY;
    return 0;
}
off_t _lseek(int descriptor, off_t offset, int origin)
{
    (void)descriptor;
    (void)offset;
    (void)origin;
    errno = ESPIPE;
    return (off_t)-1;
}
ssize_t _read(int descriptor, void *buffer, size_t length)
{
    (void)descriptor;
    (void)buffer;
    (void)length;
    errno = EBADF;
    return -1;
}
ssize_t _write(int descriptor, const void *buffer, size_t length)
{
    (void)descriptor;
    (void)buffer;
    (void)length;
    errno = EBADF;
    return -1;
}
int _getpid(void) { return 1; }
int _kill(int process, int signal)
{
    (void)process;
    (void)signal;
    errno = ENOSYS;
    return -1;
}
void *_sbrk(ptrdiff_t increment)
{
    (void)increment;
    errno = ENOMEM;
    return (void *)-1;
}
__attribute__((noreturn)) void _exit(int status)
{
    (void)status;
    __disable_irq();
    for (;;) { __WFI(); }
}
