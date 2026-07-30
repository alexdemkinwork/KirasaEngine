using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;

namespace KirasaEngine.MGL.Rendering.Backends.OpenGL;

/// <summary>
/// Provides OpenGL error checking utilities for debugging.
/// Uses ConditionalAttribute to allow compilation out in release builds.
/// </summary>
internal static class GLErrorChecker
{
    private const string Tag = "[OpenGL]";

    /// <summary>
    /// Maximum number of consecutive errors to log before throttling.
    /// Prevents log spam when the same error repeats continuously.
    /// </summary>
    private static int _consecutiveErrorCount;
    private static GLEnum _lastErrorType;
    private static string _lastErrorLocation = string.Empty;
    private static readonly object _lock = new();

    /// <summary>
    /// Checks for OpenGL errors and logs them if found.
    /// This method is conditional on DEBUG symbol for performance.
    /// </summary>
    /// <param name="gl">The OpenGL API instance.</param>
    /// <param name="operation">Description of the operation being checked.</param>
    /// <param name="file">Source file where the check is performed.</param>
    /// <param name="line">Line number in the source file.</param>
    [Conditional("DEBUG")]
    public static void CheckError(GL gl, string operation, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var error = gl.GetError();
        if (error != GLEnum.NoError)
        {
            LogError(gl, error, operation, file, line);
        }
    }

    /// <summary>
    /// Checks for OpenGL errors without conditional compilation.
    /// Use this for critical error checking that should always run.
    /// </summary>
    public static void CheckErrorAlways(GL gl, string operation, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var error = gl.GetError();
        if (error != GLEnum.NoError)
        {
            LogError(gl, error, operation, file, line);
        }
    }

    /// <summary>
    /// Validates that a handle is non-zero (valid OpenGL object handle).
    /// </summary>
    [DebuggerHidden]
    public static void ValidateHandle(uint handle, string handleType, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (handle == 0)
        {
            throw new InvalidOperationException($"{Tag} Invalid {handleType} handle (0) at {file}:{line}");
        }
    }

    /// <summary>
    /// Logs an OpenGL error with throttling to prevent spam.
    /// </summary>
    private static void LogError(GL gl, GLEnum error, string operation, string file, int line)
    {
        lock (_lock)
        {
            // Throttle repeated errors from the same location
            var currentLocation = $"{file}:{line}";
            
            if (error == _lastErrorType && !string.IsNullOrEmpty(_lastErrorLocation) &&
                _lastErrorLocation.Contains(file) && _consecutiveErrorCount > 10)
            {
                if (_consecutiveErrorCount % 100 == 0)
                {
                    Console.Error.WriteLine(
                        $"{Tag} ERROR: {error} in '{operation}' at {file}:{line} " +
                        $"(repeated {_consecutiveErrorCount} times, throttled)");
                }
                _consecutiveErrorCount++;
                return;
            }

            if (error == _lastErrorType && _lastErrorLocation == currentLocation)
            {
                _consecutiveErrorCount++;
            }
            else
            {
                _consecutiveErrorCount = 1;
                _lastErrorType = error;
                _lastErrorLocation = currentLocation;
            }

            var errorString = GetErrorString(error);
            Console.Error.WriteLine(
                $"{Tag} ERROR: {errorString} ({error}) in '{operation}' at {file}:{line}");
        }
    }

    /// <summary>
    /// Gets a human-readable string for an OpenGL error code.
    /// </summary>
    private static string GetErrorString(GLEnum error) => error switch
    {
        GLEnum.NoError => "No Error",
        GLEnum.InvalidEnum => "Invalid Enum",
        GLEnum.InvalidValue => "Invalid Value",
        GLEnum.InvalidOperation => "Invalid Operation",
        GLEnum.StackOverflow => "Stack Overflow",
        GLEnum.StackUnderflow => "Stack Underflow",
        GLEnum.OutOfMemory => "Out of Memory",
        GLEnum.InvalidFramebufferOperation => "Invalid Framebuffer Operation",
        GLEnum.ContextLost => "Context Lost",
        _ => error.ToString()
    };

    /// <summary>
    /// Resets error throttling state. Useful when context is recreated.
    /// </summary>
    public static void ResetThrottling()
    {
        lock (_lock)
        {
            _consecutiveErrorCount = 0;
            _lastErrorType = GLEnum.NoError;
            _lastErrorLocation = string.Empty;
        }
    }

    /// <summary>
    /// Gets the current OpenGL version as a string for logging.
    /// </summary>
    public static string GetVersionString(GL gl)
    {
        unsafe
        {
            var version = gl.GetString(StringName.Version);
            var renderer = gl.GetString(StringName.Renderer);
            return $"OpenGL {SilkMarshal.PtrToString((nint)version, NativeStringEncoding.UTF8)} on {SilkMarshal.PtrToString((nint)renderer, NativeStringEncoding.UTF8)}";
        }
    }
}