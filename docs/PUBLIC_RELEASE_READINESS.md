# Public source publication

This repository starts from a reviewed source snapshot. The earlier development
history and local recovery material remain in a separate private repository.
They are not dependencies of this public source tree.

The first commit contains the portable library, designer, schemas, resources and
host tests. A subsequent commit adds the H750 starter pair with their LiTheView
submodules pinned to that library-only root. This keeps recursive dependency
initialization finite and avoids references to the earlier private Git history.

The guided starter contains the blank application and lesson support; the completed
starter contains the finished button/counter UI. Developer-created UI in the guided
starter is local exercise output. Vendor dependencies remain application-owned.

Build and validation commands are in [BUILD_ENVIRONMENT.md](BUILD_ENVIRONMENT.md),
[CONFORMANCE.md](CONFORMANCE.md), and each starter's README and VALIDATION.md.
See [the clean-history validation record](PUBLICATION_VALIDATION.md) for results
and the precise source and test scope.

STM32CubeMX is downloaded directly from
[STMicroelectronics](https://www.st.com/en/development-tools/stm32cubemx.html).
The tool is not redistributed. ST-generated source keeps its existing attribution;
the file-level evidence and limitations remain in
[the STM32 provenance inventory](../licenses/stm32-examples.md).

Historical validation notes elsewhere in the documentation describe development
checkpoints. Old commit identifiers and local evidence folders in those notes are
not prerequisites or fetchable dependencies of this new repository. Public users
should run the documented commands against the checked-out source.
