# Phi GPU investigation on GTX 1080 Ti

Tested 2026-09-28 with the app's Foundry Local WinML SDK 1.2.4, production
generation prompts, JSON schemas, correction rules, and evidence validation.
The test machine has an NVIDIA GeForce GTX 1080 Ti, 11,264 MiB VRAM, compute
capability 6.1, and NVIDIA driver 560.94. No NPU is required for these GPU paths.

## Result

Both catalog GPU variants loaded successfully but failed on their first inference.
They are not usable replacements for CPU Phi on this machine with this runtime.

| Variant | Load time | First request |
| --- | ---: | --- |
| `Phi-4-mini-instruct-cuda-gpu:5` | 3.255 s | HTTP 500: cuDNN failure 5003 in the attention-mask `ReduceSum` node |
| `Phi-4-mini-instruct-generic-gpu:5` (WebGPU) | 4.092 s | HTTP 500: the embedding `Gather` shader requires `f16`, which the device does not support |

Loading time excludes downloads and is not successful inference latency. Both
failures happened before generated text was returned. The listener and model were
released after each failure; the CUDA and WebGPU unloads completed in 66 and 73 ms.

The CPU control passed all six requests on the first attempt, including production
schema/evidence validation and checks for the expected fixture subject. Model
loading took 9.150 seconds; listener startup took 0.015 seconds. Generation times:

| Synthetic fixture | Name | Summary | Alt text |
| --- | ---: | ---: | ---: |
| Invoice | 11.730 s | 13.700 s | 12.562 s |
| Dense code/error text | 24.520 s | 28.820 s | 28.409 s |

These are individual control timings, not a statistical performance benchmark.
GPU generation produced no successful measurement to compare against them.
The diagnostic build passed with zero warnings or errors.

The CUDA provider downloaded cuDNN 9.20.0.48, ONNX Runtime CUDA
1.26.20260520.2, ONNX Runtime GenAI CUDA 0.14.1, and CUDA dependencies 12.8.3.
NVIDIA [removed Pascal support in cuDNN 9.11](https://docs.nvidia.com/deeplearning/cudnn/backend/v9.20.0/release-notes.html#cudnn-9-11-0).
The installed cuDNN is therefore outside its supported hardware range on this
card. A driver update alone cannot restore that removed support.
The WebGPU error identifies a different device feature limitation in the shipped
model. Microsoft's [WebGPU documentation](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/webgpu-ep)
also identifies this execution provider as experimental.

TensorRT RTX was not offered by provider discovery. Microsoft's current
[requirements](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers#nvtensorrtrtx-nvidia)
list RTX 30-series and newer GPUs for that Windows ML provider.

## Discovery and preparation

Initially `DiscoverEps()` returned `CUDAExecutionProvider` and
`WebGpuExecutionProvider`, both unregistered. The catalog showed only CPU variants.
Explicit `DownloadAndRegisterEpsAsync` calls prepared each provider and exposed
its GPU models. This registration must run in each process even after downloading
the binaries. The app currently selects CPU explicitly and does not perform this
GPU preparation.

Catalog presence and successful model loading are insufficient compatibility
checks: both GPU variants passed those stages and failed when executing a node.
Any future automatic GPU selection needs an inference qualification or controlled
CPU fallback, with cleanup completed before loading the replacement model.

## Reproduction and scope

The smoke harness now provides `--device-probe`, `--prepare-ep`, `--device-check`,
and `--prepare-model`; see its [README](../tools/CaptureTool.Analysis.Smoke/README.md).
The device check uses synthetic invoice and dense-code metadata for separate name,
summary, and alt-text requests. It reports model loading separately from generation,
token counts, retries, and validated output. Raw synthetic responses remain under
the diagnostic output's `devices` directory. No user captures are read.

Logs:

- `artifacts/gpu-device-probe.log`
- `artifacts/gpu-cuda-prepare.log`
- `artifacts/gpu-webgpu-prepare.log`
- `artifacts/gpu-phi-cuda.log`
- `artifacts/gpu-phi-webgpu.log`
- `artifacts/gpu-phi-cpu-baseline.log`

The app's production device selection and package versions remain as before this
investigation. No GPU driver or machine-wide CUDA installation was changed.
Provider binaries and the two GPU model variants are retained in the isolated
test cache for reproducibility. Testing newer GPUs is still required before
qualifying accelerated execution for release. Supporting Pascal through another
runtime or older dependencies would need a separate compatibility evaluation.
