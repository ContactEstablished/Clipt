using System.Runtime.InteropServices;
using Clipt.Interop;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Services;

public sealed class SendInputPasteService(ILogger<SendInputPasteService> logger) : IInputSimulator
{
    public Task<bool> SendPasteAsync(nint targetHwnd, CancellationToken cancellationToken)
    {
        if (targetHwnd == 0)
        {
            logger.LogDebug("No target window handle; skipping paste.");
            return Task.FromResult(false);
        }

        if (!NativeMethods.IsWindow(targetHwnd))
        {
            logger.LogDebug("Target window {Hwnd} is no longer valid; skipping paste.", targetHwnd);
            return Task.FromResult(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Attach this thread's input to the target so SetForegroundWindow
            // is more likely to succeed (e.g. when Clipt is in the foreground).
            var foreThreadId = NativeMethods.GetWindowThreadProcessId(targetHwnd, out _);
            var currentThreadId = NativeMethods.GetCurrentThreadId();
            var attached = NativeMethods.AttachThreadInput(
                (nint)currentThreadId, (nint)foreThreadId, true);

            try
            {
                NativeMethods.SetForegroundWindow(targetHwnd);
                // Brief pause to let the target window become foreground before
                // injecting input events.
                Thread.Sleep(50);
            }
            finally
            {
                if (attached)
                {
                    NativeMethods.AttachThreadInput(
                        (nint)currentThreadId, (nint)foreThreadId, false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            SendCtrlV();

            logger.LogDebug("Sent Ctrl+V to window {Hwnd}.", targetHwnd);
            return Task.FromResult(true);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send Ctrl+V to window {Hwnd}.", targetHwnd);
            return Task.FromResult(false);
        }
    }

    private void SendCtrlV()
    {
        var size = Marshal.SizeOf<Input>();

        var inputs = new[]
        {
            // Ctrl down
            new Input
            {
                Type = NativeMethods.InputKeyboard,
                U = new InputUnion
                {
                    Ki = new KeyboardInput
                    {
                        WVk = NativeMethods.VkControl,
                        DwFlags = 0,
                    },
                },
            },
            // V down
            new Input
            {
                Type = NativeMethods.InputKeyboard,
                U = new InputUnion
                {
                    Ki = new KeyboardInput
                    {
                        WVk = NativeMethods.VkV,
                        DwFlags = 0,
                    },
                },
            },
            // V up
            new Input
            {
                Type = NativeMethods.InputKeyboard,
                U = new InputUnion
                {
                    Ki = new KeyboardInput
                    {
                        WVk = NativeMethods.VkV,
                        DwFlags = NativeMethods.KeyeventfKeyup,
                    },
                },
            },
            // Ctrl up
            new Input
            {
                Type = NativeMethods.InputKeyboard,
                U = new InputUnion
                {
                    Ki = new KeyboardInput
                    {
                        WVk = NativeMethods.VkControl,
                        DwFlags = NativeMethods.KeyeventfKeyup,
                    },
                },
            },
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, size);
        if (sent != inputs.Length)
        {
            logger.LogDebug(
                "SendInput returned {Sent} out of {Expected} events.",
                sent,
                inputs.Length);
        }
    }
}
