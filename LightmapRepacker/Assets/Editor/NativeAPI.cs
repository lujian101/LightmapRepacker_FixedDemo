#if UNITY_ANDROID && !UNITY_EDITOR
#define ANDROID_RUNTIME
#endif

#if UNITY_IPHONE && !UNITY_EDITOR
#define IOS_RUNTIME
#endif

#if !UNITY_EDITOR && ( UNITY_IPHONE || UNITY_ANDROID )
#define MOBILE_RUNTIME
#endif

using System;
using System.Runtime.InteropServices;
using AOT;
using Common;

namespace AresEditor {

    public unsafe static class NativeAPI {

#if IOS_RUNTIME
        public const String Name = "__Internal";
#else
        public const String Name = "FGDKit_NativeCore";
#endif
        #region crt
        [DllImport( Name, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FGDKit_Free" )]
        public static extern void crt_free( IntPtr p );

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FGDKit_Malloc" )]
        public static extern IntPtr crt_malloc( Int32 size );
        #endregion

        #region stb
        // 16 bytes, nominally
        [StructLayout( LayoutKind.Sequential )]
        public struct stbrp_rect {
            // reserved for your use:
            public Int32 id;
            // input:
            public UInt16 w, h;
            // output:
            public UInt16 x, y;
            public Int32 was_packed;  // non-zero if valid packing
        }

        [StructLayout( LayoutKind.Sequential )]
        public unsafe struct stbrp_node {
            public UInt16 x, y;
            public stbrp_node* next;
        }

        [StructLayout( LayoutKind.Sequential )]
        public unsafe struct stbrp_context {
            public Int32 width;
            public Int32 height;
            public Int32 align;
            public Int32 init_mode;
            public Int32 heuristic;
            public Int32 num_nodes;
            public stbrp_node* active_head;
            public stbrp_node* free_head;
            public stbrp_node extra0; // we allocate two extra nodes so optimal user-node-count is 'width' not 'width+2'
            public stbrp_node extra1; // we allocate two extra nodes so optimal user-node-count is 'width' not 'width+2'
        }

        public enum STBRP_HEURISTIC {
            Skyline_default = 0,
            Skyline_BL_sortHeight = Skyline_default,
            Skyline_BF_sortHeight,
        }

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        static extern IntPtr stbi_load( String filename, out int x, out int y, out int comp, int req_comp );

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        static extern IntPtr stbi_load_from_memory( IntPtr buffer, int len, out int x, out int y, out int comp, int req_comp );

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        static extern void stbi_image_free( IntPtr retval_from_stbi_load );

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        static extern IntPtr stbi_failure_reason();

        // Assign packed locations to rectangles. The rectangles are of type
        // 'stbrp_rect' defined below, stored in the array 'rects', and there
        // are 'num_rects' many of them.
        //
        // Rectangles which are successfully packed have the 'was_packed' flag
        // set to a non-zero value and 'x' and 'y' store the minimum location
        // on each axis (i.e. bottom-left in cartesian coordinates, top-left
        // if you imagine y increasing downwards). Rectangles which do not fit
        // have the 'was_packed' flag set to 0.
        //
        // You should not try to access the 'rects' array from another thread
        // while this function is running, as the function temporarily reorders
        // the array while it executes.
        //
        // To pack into another rectangle, you need to call stbrp_init_target
        // again. To continue packing into the same rectangle, you can call
        // this function again. Calling this multiple times with multiple rect
        // arrays will probably produce worse packing results than calling it
        // a single time with the full rectangle array, but the option is
        // available.
        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static unsafe extern void stbrp_pack_rects( stbrp_context* context, stbrp_rect* rects, int num_rects );

        // Initialize a rectangle packer to:
        //    pack a rectangle that is 'width' by 'height' in dimensions
        //    using temporary storage provided by the array 'nodes', which is 'num_nodes' long
        //
        // You must call this function every time you start packing into a new target.
        //
        // There is no "shutdown" function. The 'nodes' memory must stay valid for
        // the following stbrp_pack_rects() call (or calls), but can be freed after
        // the call (or calls) finish.
        //
        // Note: to guarantee best results, either:
        //       1. make sure 'num_nodes' >= 'width'
        //   or  2. call stbrp_allow_out_of_mem() defined below with 'allow_out_of_mem = 1'
        //
        // If you don't do either of the above things, widths will be quantized to multiples
        // of small integers to guarantee the algorithm doesn't run out of temporary storage.
        //
        // If you do #2, then the non-quantized algorithm will be used, but the algorithm
        // may run out of temporary storage and be unable to pack some rectangles.
        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static unsafe extern void stbrp_init_target( stbrp_context* context, int width, int height, stbrp_node* nodes, int num_nodes );

        // Optionally call this function after init but before doing any packing to
        // change the handling of the out-of-temp-memory scenario, described above.
        // If you call init again, this will be reset to the default (false).
        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static unsafe extern void stbrp_setup_allow_out_of_mem( stbrp_context* context, int allow_out_of_mem );

        // Optionally select which packing heuristic the library should use. Different
        // heuristics will produce better/worse results for different data sets.
        // If you call init again, this will be reset to the default.
        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static unsafe extern void stbrp_setup_heuristic( stbrp_context* context, int heuristic );

        public static Byte[] stbi_loadfromfile( String filename, out int x, out int y, out int comp, int req_comp = 0 ) {
            Byte[] data = null;
            var ptr = IntPtr.Zero;
            x = 0;
            y = 0;
            comp = 0;
            try {
                ptr = stbi_load( filename, out x, out y, out comp, req_comp );
                if ( ptr != IntPtr.Zero ) {
                    var size = x * y * comp;
                    if ( size > 0 ) {
                        data = new byte[ size ];
                        Marshal.Copy( ptr, data, 0, size );
                    }
                }
            } catch ( Exception e ) {
                if ( !( e is DllNotFoundException ) ) {
                    UDebug.LogException( e );
                }
            } finally {
                if ( ptr != IntPtr.Zero ) {
                    stbi_image_free( ptr );
                } else {
                    var errorPtr = stbi_failure_reason();
                    for ( ; ; ) {
                        if ( errorPtr != IntPtr.Zero ) {
                            var error = Marshal.PtrToStringAnsi( errorPtr );
                            if ( !String.IsNullOrEmpty( error ) ) {
                                UDebug.LogError( "stbi_loadfromfile failed: {0}", error );
                                break;
                            }
                        }
                        UDebug.LogError( "stbi_loadfromfile failed: reason unknown." );
                        break;
                    }
                }
            }
            return data;
        }

        public static bool stbi_loadfromfilex( String filename, Action<IntPtr, int, int, int> callback, out int x, out int y, out int comp, int req_comp = 0 ) {
            var ptr = IntPtr.Zero;
            x = 0;
            y = 0;
            comp = 0;
            try {
                ptr = stbi_load( filename, out x, out y, out comp, req_comp );
                if ( callback != null ) {
                    callback( ptr, x, y, comp );
                }
            } catch ( Exception e ) {
                if ( !( e is DllNotFoundException ) ) {
                    UDebug.LogException( e );
                }
            } finally {
                if ( ptr != IntPtr.Zero ) {
                    stbi_image_free( ptr );
                }
            }
            return ptr != IntPtr.Zero;
        }

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static extern int stbi_write_png( String filename, int w, int h, int comp, IntPtr data, int stride_in_bytes );

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static extern int stbi_write_bmp( String filename, int w, int h, int comp, IntPtr data );

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static extern int stbi_write_tga( String filename, int w, int h, int comp, IntPtr data );

        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static extern int stbi_write_hdr( String filename, int w, int h, int comp, IntPtr data );
        #endregion

        #region tinyexr
        // Loads single-frame OpenEXR image. Assume EXR image contains A(single channel
        // alpha) or RGB(A) channels.
        // Application must free image data as returned by `out_rgba`
        // Result image format is: float x RGBA x width x hight
        // Returns negative value and may set error string in `err` when there's an
        // error
        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static extern Int32 LoadEXR( ref IntPtr out_rgba, out Int32 width, out Int32 height, String filename, ref IntPtr err );

        // Saves single-frame OpenEXR image. Assume EXR image contains RGB(A) channels.
        // components must be 1(Grayscale), 3(RGB) or 4(RGBA).
        // Input image format is: `float x width x height`, or `float x RGB(A) x width x
        // hight`
        // Save image as fp16(HALF) format when `save_as_fp16` is positive non-zero
        // value.
        // Save image as fp32(FLOAT) format when `save_as_fp16` is 0.
        [DllImport( Name, CallingConvention = CallingConvention.Cdecl )]
        public static extern Int32 SaveEXR( IntPtr data, Int32 width, Int32 height, Int32 components, Int32 save_as_fp16, String filename );
        #endregion
    }
}
