using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Threading;
using System.Diagnostics;

namespace AresEditor.ArtistKit {

    public class LightmapViewer : EditorWindow {

        struct Vector4d {
            public double x, y, z, w;
            public Vector4d( double _x, double _y, double _z, double _w ) {
                x = _x;
                y = _y;
                z = _z;
                w = _w;
            }
        }

        static LightmapViewer m_window = null;
        static Dictionary<String, Vector2[]> _MeshUVsCache = null;
        static Dictionary<Renderer, Vector4d> _LightmapUVBoundsCache = null;

        List<KeyValuePair<int, Vector4d>> _litmapUVs = null;
        static Texture2D _redTex = null;

        [MenuItem( "Tools/LightmapViewer" )]
        static void Init() {
            if ( m_window == null ) {
                m_window = ( LightmapViewer )EditorWindow.GetWindow( typeof( LightmapViewer ), false, "Lightmap Viewer" );
            }
            m_window.minSize = new Vector2( 400, 300 );
            m_window.position = new Rect( 0, 0, m_window.minSize.x, m_window.minSize.y );
            m_window.Show();
        }

        void OnEnable() {
            Selection.selectionChanged += OnSelectionChanged;
            UpdateCache();
        }

        void OnDisable() {
            _litmapUVs = null;
            Selection.selectionChanged -= OnSelectionChanged;
            if ( _MeshUVsCache != null ) {
                _MeshUVsCache.Clear();
            }
            if ( _LightmapUVBoundsCache != null ) {
                _LightmapUVBoundsCache.Clear();
            }
            if ( _redTex != null ) {
                UnityEngine.Object.DestroyImmediate( _redTex );
                _redTex = null;
            }
        }

        void OnDestroy() {
            m_window = null;
        }

        void UpdateCache() {
            _MeshUVsCache = _MeshUVsCache ?? new Dictionary<String, Vector2[]>();
            _LightmapUVBoundsCache = _LightmapUVBoundsCache ?? new Dictionary<Renderer, Vector4d>();
            var objs = Selection.gameObjects;
            List<KeyValuePair<int, Vector4d>> litmapUVs = null;
            for ( int i = 0; i < objs.Length; ++i ) {
                var rs = objs[ i ].GetComponentsInChildren<Renderer>();
                for ( int j = 0; j < rs.Length; ++j ) {
                    var r = rs[ j ];
                    if ( r != null && r.lightmapIndex >= 0 ) {
                        Mesh m = null;
                        if ( r is MeshRenderer ) {
                            var mf = r.GetComponent<MeshFilter>();
                            if ( mf != null ) {
                                m = mf.sharedMesh;
                            }
                        } else if ( r is SkinnedMeshRenderer ) {
                            var _r = r as SkinnedMeshRenderer;
                            m = _r.sharedMesh;
                        }

                        if ( m != null ) {
                            Vector4d uvBounds;
                            if ( !_LightmapUVBoundsCache.TryGetValue( r, out uvBounds ) ) {
                                var uv = GetMeshUV2( m );
                                if ( uv != null ) {
                                    var __uv = new Vector2[ uv.Length ];
                                    Array.Copy( uv, __uv, uv.Length );
                                    uv = __uv;
                                    litmapUVs = litmapUVs ?? new List<KeyValuePair<int, Vector4d>>();
                                    var minx = double.MaxValue;
                                    var miny = double.MaxValue;
                                    var maxx = double.MinValue;
                                    var maxy = double.MinValue;
                                    for ( var _j = 0; _j < uv.Length; ++_j ) {
                                        uv[ _j ].x *= r.lightmapScaleOffset.x;
                                        uv[ _j ].y *= r.lightmapScaleOffset.y;
                                        uv[ _j ].x += r.lightmapScaleOffset.z;
                                        uv[ _j ].y += r.lightmapScaleOffset.w;
                                        uv[ _j ].y = 1 - uv[ _j ].y;
                                        var _uv = uv[ _j ];
                                        if ( _uv.x < minx ) {
                                            minx = _uv.x;
                                        }
                                        if ( _uv.y < miny ) {
                                            miny = _uv.y;
                                        }
                                        if ( _uv.x > maxx ) {
                                            maxx = _uv.x;
                                        }
                                        if ( _uv.y > maxy ) {
                                            maxy = _uv.y;
                                        }
                                    }
                                    var bounds = new Vector4d( minx, miny, maxx, maxy );
                                    litmapUVs.Add( new KeyValuePair<int, Vector4d>( r.lightmapIndex, bounds ) );
                                    _LightmapUVBoundsCache.Add( r, bounds );
                                }
                            } else {
                                litmapUVs = litmapUVs ?? new List<KeyValuePair<int, Vector4d>>();
                                litmapUVs.Add( new KeyValuePair<int, Vector4d>( r.lightmapIndex, uvBounds ) );
                            }
                        }
                    }
                }
            }
            _litmapUVs = litmapUVs;
        }

        void OnSelectionChanged() {
            if ( m_window != null ) {
                m_window.Repaint();
                UpdateCache();
            }
        }

        static Vector2[] GetMeshUV2( Mesh mesh ) {
            Vector2[] ret = null;
            var assetPath = AssetDatabase.GetAssetPath( mesh );
            var ti = AssetImporter.GetAtPath( assetPath ) as ModelImporter;
            var id = String.Format( "{0}, {1}", assetPath, mesh.GetInstanceID() );
            if ( !String.IsNullOrEmpty( assetPath ) ) {
                if ( _MeshUVsCache.TryGetValue( id, out ret ) && ret != null ) {
                    return ret;
                }
                if ( mesh.isReadable == false ) {
                    if ( ti != null ) {
                        if ( ti.isReadable ) {
                            ret = mesh.uv2;
                            if ( ret.Length == 0 ) {
                                ret = mesh.uv;
                            }
                        } else {
                            try {
                                ti.isReadable = true;
                                AssetDatabase.ImportAsset( assetPath );
                                ret = mesh.uv2;
                                if ( ret.Length == 0 ) {
                                    ret = mesh.uv;
                                }
                            } finally {
                                ti.isReadable = false;
                                AssetDatabase.ImportAsset( assetPath );
                            }
                        }
                    }
                } else {
                    ret = mesh.uv2;
                    if ( ret.Length == 0 ) {
                        ret = mesh.uv;
                    }
                }
                _MeshUVsCache[ id ] = ret;
            }
            return ret;
        }

        Rect OnGUI_Lightmap() {
            Rect selectRect = new Rect();
            var lightmaps = LightmapSettings.lightmaps;
            if ( lightmaps != null && lightmaps.Length > 0 ) {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField( "" );
                var width = 0.0f;
                var height = 0.0f;
                if ( Event.current.type == EventType.Repaint ) {
                    var rt = GUILayoutUtility.GetLastRect();
                    var x = rt.x;
                    var y = rt.y;
                    var gap = 4;
                    var border = x;
                    width = border * 2;
                    height = y + border;
                    var maxTexHeight = 0;
                    for ( int i = 0; i < lightmaps.Length; ++i ) {
                        if ( i > 0 ) {
                            width += gap;
                            x += gap;
                        }
                        var m = lightmaps[ i ];
                        var t = m.lightmapLight;
                        var t_width = t.width;
                        var t_height = t.height;
                        if ( t_height > maxTexHeight ) {
                            maxTexHeight = t_height;
                        }
                        if ( t != null ) {
                            EditorGUI.DrawPreviewTexture( new Rect( x, y, t_width, t_height ), t );
                            if ( _litmapUVs != null ) {
                                for ( int j = 0; j < _litmapUVs.Count; ++j ) {
                                    var uv = _litmapUVs[ j ];
                                    if ( uv.Key == i ) {
                                        var bounds = uv.Value;
                                        double fMinX = bounds.x * t_width;
                                        double fMaxX = bounds.z * t_width;
                                        double fMinY = bounds.y * t_height;
                                        double fMaxY = bounds.w * t_height;
                                        // 绘制lightmap像素框的时候，分别取整到最大包围盒
                                        int pixMinX = ( int )Math.Max( Math.Floor( bounds.x * t_width ), 0 );
                                        int pixMaxX = ( int )Math.Min( Math.Ceiling( bounds.z * t_width ), t_width );
                                        int pixMinY = ( int )Math.Max( Math.Floor( bounds.y * t_height ), 0 );
                                        int pixMaxY = ( int )Math.Min( Math.Ceiling( bounds.w * t_height ), t_height );
                                        var color = Color.red;
                                        var _color = GUI.color;
                                        var rect = new Rect( pixMinX, pixMinY, pixMaxX - pixMinX, pixMaxY - pixMinY );
                                        var uiRect = rect;
                                        uiRect.x += x;
                                        uiRect.y += y;
                                        var mouseOvered = uiRect.Contains( Event.current.mousePosition );
                                        color = mouseOvered ? Color.green : Color.red;
                                        color.a = mouseOvered ? 0.0f : 0.5f;
                                        GUI.color = color;
                                        var drawRT = new Rect();
                                        drawRT.xMin = pixMinX;
                                        drawRT.xMax = pixMaxX;
                                        drawRT.yMin = pixMinY;
                                        drawRT.yMax = pixMaxY;
                                        drawRT.x += x;
                                        drawRT.y += y;
                                        if ( _redTex == null ) {
                                            _redTex = new Texture2D( 1, 1, TextureFormat.RGBA32, false );
                                            _redTex.SetPixel( 0, 0, new Color( 1, 0, 0, 0.2f ) );
                                            _redTex.Apply();
                                        }
                                        EditorGUI.DrawTextureTransparent( drawRT, _redTex, ScaleMode.StretchToFill );
                                        color = mouseOvered ? Color.green : Color.red;
                                        color.a = mouseOvered ? 0.5f : 0.0f;
                                        if ( mouseOvered ) {
                                            // 修复误差，用于显示真实的uv框
                                            var errorMinX = ( float )( fMinX - pixMinX );
                                            var errorMinY = ( float )( fMinY - pixMinY );
                                            var errorMaxX = ( float )( fMaxX - pixMaxX );
                                            var errorMaxY = ( float )( fMaxY - pixMaxY );
                                            selectRect = rect;
                                            selectRect.xMin += errorMinX;
                                            selectRect.yMin += errorMinY;
                                            selectRect.xMax += errorMaxX;
                                            selectRect.yMax += errorMaxY;
                                        }
                                        GUI.color = color;
                                        var frameRT = drawRT;
                                        // 稍微扩大一点，刚好包围完整
                                        frameRT.x -= 1;
                                        frameRT.y -= 1;
                                        frameRT.width += 1;
                                        frameRT.height += 1;
                                        EditorGUI.DrawRect( new Rect( frameRT.xMin, frameRT.yMin, frameRT.xMax - frameRT.xMin, 1 ), color );
                                        EditorGUI.DrawRect( new Rect( frameRT.xMin, frameRT.yMin, 1, frameRT.yMax - frameRT.yMin ), color );
                                        EditorGUI.DrawRect( new Rect( frameRT.xMin, frameRT.yMax, frameRT.xMax - frameRT.xMin, 1 ), color );
                                        EditorGUI.DrawRect( new Rect( frameRT.xMax, frameRT.yMin, 1, frameRT.yMax - frameRT.yMin ), color );
                                        GUI.color = _color;
                                    }
                                }
                            }
                            width += t_width;
                            x += t_width;
                        }
                    }
                    height += maxTexHeight;
                    if ( m_window != null ) {
                        var size = m_window.minSize;
                        if ( width > size.x ) {
                            size.x = width;
                        }
                        if ( height > size.y ) {
                            size.y = height;
                        }
                        m_window.minSize = size;
                    }
                }
                EditorGUILayout.LabelField(
                    String.Format( "PixelRect: ( {0}, {1}, {2}, {3} )",
                        selectRect.x, selectRect.y, selectRect.width, selectRect.height )
                );
                EditorGUILayout.EndVertical();
            }
            return selectRect;
        }

        void OnGUI() {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField( "Current Selected:", Selection.activeGameObject, typeof( GameObject ), true );
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            OnGUI_Lightmap();
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }
    }
}
