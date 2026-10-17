// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using LitheView.Designer.Core.Profiles;
using LitheView.Designer.Core.Projects;

namespace LitheView.Designer.Service.Tests;

internal static partial class Program
{
    private static async Task CheckProjectSetupAsync(DesignerService service, string application)
    {
        Directory.CreateDirectory(application);
        foreach (string name in new[] { "rgb", "synthetic-r2", "synthetic-red-cell", "synthetic-red-yellow" })
        {
            bool rgb = name == "rgb";
            ServiceResponse created = await service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest
            {
                Id = ++_requestId, ProtocolVersion = 1, Method = "project.create", Params = new() { Project = new()
                {
                    ApplicationRoot = application, ProjectDirectory = Path.Combine(application, name),
                    ProjectId = "test.setup." + name, DisplayName = "Project setup",
                    SurfaceId = rgb ? "litheview.surface.stm32h750b-dk-rk043fn48h-rgb565" : null,
                    NativeDisplayId = rgb ? null : "litheview.definition.software." + name,
                    LogicalInput = "outputOnly", ComponentDirectory = name + "/Generated", BuildDirectory = "build/" + name
                } }
            }, Protocol.JsonOptions)).ConfigureAwait(false);
            Require(created.Ok, "Setup fixture created: " + created.Error?.Message);
            string manifest = Result(created).GetProperty("manifestPath").GetString()!;
            string form = Result(created).GetProperty("formPath").GetString()!;
            string xml = File.ReadAllText(form);
            string handler = Path.Combine(Path.GetDirectoryName(manifest)!, "form_handlers.hpp");
            File.WriteAllText(handler, "// owned by the application");
            ValueTask<ServiceResponse> Request(ProjectSetupRequest? setup = null, string method = "project.setup") =>
                service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest
                {
                    Id = ++_requestId, ProtocolVersion = 1, Method = method,
                    Params = new() { ManifestPath = manifest, FormPath = form, Xml = xml, DocumentVersion = 11, Setup = setup }
                }, Protocol.JsonOptions));
            static void Apply(ServiceResponse result)
            {
                Require(result.Ok, "Setup proposal succeeds: " + result.Error?.Message);
                foreach (JsonElement document in Result(result).GetProperty("documents").EnumerateArray())
                    Require(File.ReadAllText(document.GetProperty("path").GetString()!) == document.GetProperty("before").GetString(),
                        "The service does not write project settings.");
                foreach (JsonElement edit in Result(result).GetProperty("edits").EnumerateArray())
                    File.WriteAllText(edit.GetProperty("path").GetString()!, edit.GetProperty("after").GetString());
            }
            static ProjectSetupRequest RoundTrip(JsonElement current)
            {
                DisplayDefinitionProfile definition = DisplayDefinitionProfileLoader.Load(
                    System.Text.Encoding.UTF8.GetBytes(current.GetProperty("displayDefinition").GetRawText())).Profile!;
                return new()
                {
                    Snapshot = current.GetProperty("snapshot").GetString()!,
                    CustomSurface = current.TryGetProperty("displaySurface", out JsonElement surface) ? surface.Clone() : null,
                    CustomNativeDisplay = current.TryGetProperty("displaySurface", out _) ? null : new()
                    {
                        Format = current.GetProperty("displayDefinition").GetProperty("format").GetString()!,
                        Width = definition.Geometry.Width, Height = definition.Geometry.Height,
                        Shape = "rectangle", SafeArea = definition.Geometry.SafeArea, SourceReference = definition.Source.Reference
                    }
                };
            }
            ServiceResponse initial = await Request().ConfigureAwait(false);
            Require(initial.Ok && Result(initial).GetProperty("documents").GetArrayLength() == (rgb ? 4 : 3), "Setup reads the complete source configuration.");
            ProjectSetupRequest original = RoundTrip(Result(initial));
            ServiceResponse noOp = await Request(original).ConfigureAwait(false);
            Require(noOp.Ok && Result(noOp).GetProperty("edits").GetArrayLength() == 0, "Unchanged RGB and native display settings preserve all bytes and provenance.");

            ProjectSetupRequest touch = original with { Input = new() { Pointer = true, Keys = true, Text = true, Encoder = true, Select = true } };
            ServiceResponse inputPlan = await Request(touch).ConfigureAwait(false);
            Require(inputPlan.Ok && Result(inputPlan).GetProperty("edits").GetArrayLength() == 2, "Input-only setup edits only input and its manifest lock.");
            Apply(inputPlan);
            ServiceResponse stale = await Request(touch).ConfigureAwait(false);
            Require(!stale.Ok && stale.Error?.Code == "ProjectChanged", "A dialog opened before another saved setup cannot overwrite it.");
            ServiceResponse current = await Request().ConfigureAwait(false);
            Require(current.Ok && Result(current).GetProperty("input").GetProperty("text").GetBoolean(), "Non-preset logical input survives reopening.");

            ProjectSetupRequest changed = RoundTrip(Result(current));
            if (rgb)
            {
                DisplaySurfaceProfile surface = DisplaySurfaceProfileLoader.Load(System.Text.Encoding.UTF8.GetBytes(changed.CustomSurface!.Value.GetRawText())).Profile!;
                surface = surface with { PixelSize = surface.PixelSize with { Width = 640 },
                    SafeArea = surface.SafeArea with { Width = 640 },
                    PainterSurface = new() { Format = SurfacePixelFormat.Xrgb8888, ColorDepthBits = 24, StorageBitsPerPixel = 32 } };
                changed = changed with { CustomSurface = JsonSerializer.Deserialize<JsonElement>(DisplaySurfaceCanonicalizer.Canonicalize(surface)) };
            }
            else changed = changed with { CustomNativeDisplay = changed.CustomNativeDisplay! with { Width = 640 } };
            ServiceResponse displayPlan = await Request(changed).ConfigureAwait(false);
            Require(displayPlan.Ok && Result(displayPlan).GetProperty("edits").GetArrayLength() == (rgb ? 3 : 2), "Display-only setup keeps input unchanged.");
            Apply(displayPlan);
            ServiceResponse inspected = await Request(method: "inspect").ConfigureAwait(false);
            Require(inspected.Ok && Result(inspected).GetProperty("surface").GetProperty("width").GetInt32() == 640, "Inspector uses the reconfigured display dimensions.");
            Require(File.ReadAllText(form) == xml && File.ReadAllText(handler) == "// owned by the application", "Setup never scales form coordinates or rewrites handlers.");
            ServiceResponse exported = await Request(method: "export").ConfigureAwait(false);
            Require(!exported.Ok && exported.Error!.Message.Contains("root frame", StringComparison.Ordinal), "Generate explains that the authored root must match the new extent.");
            JsonElement root = Result(inspected).GetProperty("components")[0];
            ServiceResponse resized = await service.HandleAsync(JsonSerializer.Serialize(new ServiceRequest
            {
                Id = ++_requestId, ProtocolVersion = 1, Method = "edit", Params = new()
                {
                    ManifestPath = manifest, FormPath = form, Xml = xml, DocumentVersion = 11,
                    Operation = new() { Kind = "frame", ComponentId = root.GetProperty("id").GetString(),
                        Frame = new(0, 0, 640, root.GetProperty("frame").GetProperty("height").GetInt32()) }
                }
            }, Protocol.JsonOptions)).ConfigureAwait(false);
            Require(resized.Ok, "The developer can explicitly resize the root after setup: " + resized.Error?.Message);
            xml = Result(resized).GetProperty("xml").GetString()!;
            File.WriteAllText(form, xml);
            exported = await Request(method: "export").ConfigureAwait(false);
            Require(exported.Ok, "Generate UI uses the new display definition: " + exported.Error?.Message);
            string definitionPath = Path.Combine(application, name, "Generated/display-definition.json");
            Require(DisplayDefinitionProfileLoader.LoadFile(definitionPath).Profile!.Geometry.Width == 640, "Generated display snapshot uses the new extent.");

            current = await Request().ConfigureAwait(false);
            ProjectSetupRequest shrink = RoundTrip(Result(current));
            if (rgb)
            {
                DisplaySurfaceProfile profile = DisplaySurfaceProfileLoader.Load(System.Text.Encoding.UTF8.GetBytes(shrink.CustomSurface!.Value.GetRawText())).Profile!;
                profile = profile with { PixelSize = profile.PixelSize with { Width = 24, Height = 24 }, SafeArea = new() { X = 0, Y = 0, Width = 24, Height = 24 } };
                shrink = shrink with { CustomSurface = JsonSerializer.Deserialize<JsonElement>(DisplaySurfaceCanonicalizer.Canonicalize(profile)) };
            }
            else shrink = shrink with { CustomNativeDisplay = shrink.CustomNativeDisplay! with { Width = 24, Height = 24, SafeArea = null } };
            Apply(await Request(shrink).ConfigureAwait(false));
            ServiceResponse incompatible = await Request(method: "export").ConfigureAwait(false);
            Require(!incompatible.Ok, "A form larger than its changed display cannot silently export.");
            Require(File.ReadAllText(form) == xml, "A display shrink preserves authored controls for manual layout correction.");
            // Setup must stay available to fix a layout/display mismatch.
            current = await Request().ConfigureAwait(false);
            Apply(await Request(changed with { Snapshot = Result(current).GetProperty("snapshot").GetString()! }).ConfigureAwait(false));
            Require((await Request(method: "export").ConfigureAwait(false)).Ok, "Restoring the display makes the preserved form exportable again.");
            if (!rgb)
            {
                current = await Request().ConfigureAwait(false);
                ProjectSetupRequest alternate = RoundTrip(Result(current));
                string alternateFormat = alternate.CustomNativeDisplay!.Format == "r2Packed2x2" ? "rrrr2222" : "r2Packed2x2";
                alternate = alternate with { CustomNativeDisplay = alternate.CustomNativeDisplay with { Format = alternateFormat } };
                Apply(await Request(alternate).ConfigureAwait(false));
                Require((await Request(method: "export").ConfigureAwait(false)).Ok, "A packed-format change regenerates native storage without scaling form coordinates.");
                Require(DisplayDefinitionProfileLoader.LoadFile(definitionPath).Profile!.AddressSpace ==
                    (alternateFormat == "r2Packed2x2" ? DisplayAddressSpace.Emitters : DisplayAddressSpace.Cells), "The generated definition uses the selected coordinate units.");
            }
            string inputPath = Path.Combine(application, name, "profiles/logical-input.json");
            File.AppendAllText(inputPath, "invalid");
            Require(!(await Request().ConfigureAwait(false)).Ok, "Setup rejects tampered snapshots instead of repairing their locks.");
        }
    }
}
