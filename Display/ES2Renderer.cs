//
// ES2Renderer.cs
// RhinoMobile.Display
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using OpenTK.Graphics;
using OpenTK.Graphics.ES20;

using Rhino.DocObjects;
using RhinoMobile.Model;
using RhinoMobile.Display;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

#if __IOS__
using MonoTouch.Foundation;
using MonoTouch.OpenGLES;
#endif

#region OpenTK-1.0 API diffs
// This region handles differences between OpenTK-1.0  on MonoDroid and MonoTouch.  
// MonoDroid is behind the times and has not yet caught up with MonoTouch
// on the OpenTK-1.0 front.  Once things stabilize, this can be removed. 
// See this thread for details:
// http://forums.xamarin.com/discussion/1939/renderbuffertarget-in-opentk
// TODO: Possible fix in http://docs.xamarin.com/releases/android/xamarin.android_4/xamarin.android_4.12/#Xamarin.Android_4.12.3
#if __ANDROID__
using BufferTarget = OpenTK.Graphics.ES20.All;
using BufferUsage = OpenTK.Graphics.ES20.All;
using VertexAttribPointerType = OpenTK.Graphics.ES20.All;
using ShaderType = OpenTK.Graphics.ES20.All;
using EnableCap = OpenTK.Graphics.ES20.All;
using ProgramParameter = OpenTK.Graphics.ES20.All;
using ShaderParameter = OpenTK.Graphics.ES20.All;
using GetError = OpenTK.Graphics.ES20.All;
using Color4 = OpenTK.Graphics.ES20.All;
using DepthFunction = OpenTK.Graphics.ES20.All;
using BlendingFactorSrc = OpenTK.Graphics.ES20.All;
using BlendingFactorDest = OpenTK.Graphics.ES20.All;
using CullFaceMode = OpenTK.Graphics.ES20.All;
using FramebufferTarget = OpenTK.Graphics.ES20.All;
using RenderbufferTarget = OpenTK.Graphics.ES20.All;
using FrontFaceDirection = OpenTK.Graphics.ES20.All;
#endif
#endregion

namespace RhinoMobile.Display
{
	public class ES2Renderer : IRenderer
	{
		#region members
		private RhGLShaderProgram m_activeShader;
		#endregion

		public enum AGMColors : int
		{
			AgmRedCyan,
			AgmAmberBlue,
			AgmMagentaGreen
		}

		#region properties
		/// <value> The RMModel to be rendered. </value>
		public RMModel Model { get; private set; }

		/// <value> Frame should be passed in by the parent view. </value>
		public System.Drawing.RectangleF Frame { get; set; }

		/// <value> The ViewportInfo that is currently being rendered. </value>
		public ViewportInfo Viewport { get; set; }

		/// <value> The current material being rendered. </value>
		public DisplayMaterial CurrentMaterial { get; private set; }

		/// <value> Active shader reference used to "track" the current shader object. </value>
		public RhGLShaderProgram ActiveShader { 
			get {
				return m_activeShader;
			}

			set {
				if (m_activeShader == null) {
					m_activeShader = value;
				} else if (m_activeShader.m_hProgram != value.m_hProgram) {
					CurrentMaterial = new DisplayMaterial (); //reset the default material if we change shaders
					m_activeShader = value;
				}
			}
		}

		/// <value> Shaders are created on demand and stored in a list. </value>
		public List<RhGLShaderProgram> Shaders { get; private set; }

		/// <value> Set to true to use a fast shader on each draw call. </value>
		public bool FastDrawing { get; set; }

		#if __ANDROID__
		/// <value> This is the application context from Android which is necessary in order to retreive items from the bundle. </value>
		public Android.Content.Context AndroidContext { get; set; }
		#endif 
		#endregion

		#region constructors and disposal
		public ES2Renderer ()
		{
			Shaders = new List<RhGLShaderProgram> ();
			CurrentMaterial = new DisplayMaterial ();
		}

		/// <summary>
		/// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
		/// </summary>
		~ ES2Renderer () { Dispose (false); }

		/// <summary>
		/// Actively reclaims unmanaged resources that this instance uses.
		/// </summary>
		public void Dispose()
		{
			Dispose (true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// For derived class implementers.
		/// <para>This method is called with argument true when class user calls Dispose(), while with argument false when
		/// the Garbage Collector invokes the finalizer, or Finalize() method.</para>
		/// <para>You must reclaim all used unmanaged resources in both cases, and can use this chance to call Dispose on disposable fields if the argument is true.</para>
		/// <para>Also, you must call the base virtual method within your overriding method.</para>
		/// </summary>
		/// <param name="disposing">true if the call comes from the Dispose() method; false if it comes from the Garbage Collector finalizer.</param>
		protected virtual void Dispose(bool disposing)
		{
			// Free unmanaged resources...

			// Free managed resources...but only if called from Dispose
			// (If called from Finalize then the objects might not exist anymore)
			if (disposing) {
				if (Model != null) {
					Model.Dispose ();
					Model = null;
				}

				Frame = System.Drawing.RectangleF.Empty;

				if (Viewport != null) {
					Viewport.Dispose ();
					Viewport = null;
				}
					
				CurrentMaterial = null;

				if (ActiveShader != null) {
					GL.DeleteProgram (ActiveShader.Handle);
					ActiveShader.Disable ();
					m_activeShader = null;
				}

				if (Shaders != null) {
					Shaders.Clear ();
					Shaders = null;
				}
			}	
		}
		#endregion

		#region Shaders
		/// <summary>
		/// GetShader loads a shader resource from disk
		/// </summary>
		private RhGLShaderProgram GetShader (string baseName)
		{
			for (int i = 0; i<Shaders.Count; i++) {
				var shader = Shaders [i];
				if (shader.Name.Equals (baseName, StringComparison.InvariantCultureIgnoreCase)) {
					SetupShader (shader, Model, Viewport);
					return shader;
				}
			}
		
			String vertex_shader = GetResourceAsString (baseName, "vsh");
			String fragment_shader = GetResourceAsString (baseName, "fsh");
		
			var new_shader = RhGLShaderProgram.BuildProgram (baseName, vertex_shader, fragment_shader);
			if (new_shader != null)
				Shaders.Add (new_shader);

			SetupShader (new_shader, Model, Viewport);
			return new_shader;
		}

		/// <summary>
		/// Platform-Specific implementations
		/// <para>Android: returns the shader as a string from assets.</para>
		/// <para>iOS: returns the shader as a string from the app bundle.</para>
		/// </summary>
		private string GetResourceAsString(string name, string extension)
		{
			string resource = String.Empty;
			string contents = String.Empty;

			#if __ANDROID__
			resource = name + "." + extension;
			string shaderPath = Path.Combine ("Shaders", resource);

			var context = AndroidContext;
			System.IO.StreamReader sr = new System.IO.StreamReader (context.Assets.Open (shaderPath));
			contents = sr.ReadToEnd ();
			sr.Close();
			sr.Dispose();
			#endif

			#if __IOS__
			string shaderPath = System.IO.Path.Combine ("Shaders", name);
			resource = NSBundle.MainBundle.PathForResource(shaderPath, extension);
			contents = System.IO.File.ReadAllText (resource);
			#endif

			return contents;
		}

		/// <summary>
		/// Creates a default light and enables the active shader
		/// </summary>
		private void SetupShader (RhGLShaderProgram shader, RMModel model, ViewportInfo viewport)
		{
			// Check to make sure we actually have an active shader...
			if (shader != null) {
	
				// Enable...
				shader.Enable ();

				// Now setup and initialize frustum and lighting...
				int near, far;
				viewport.GetScreenPort (out near, out far);
				viewport.SetScreenPort ((int)Frame.Left, (int)Frame.Right, (int)Frame.Bottom, (int)Frame.Top, near, far);
				shader.SetupViewport (viewport);
				Rhino.Geometry.Light light = CreateDefaultLight ();
				shader.SetupLight (light);
				light.Dispose ();
			}
		}
		#endregion

		#region Display Methods
		/// <summary>
		/// IRenderer interface method
		/// Renders a model in a viewport
		/// </summary>
		public bool RenderModel (RMModel model, Rhino.DocObjects.ViewportInfo viewport)
		{
			Model = model;

      CheckGLError (); //HACK: necessary on some Android GPUs (don't delete this check)

			if ((model == null) || !model.IsReadyForRendering)
				return false;

			viewport.SetFrustumNearFar (model.BBox);

			// Disable Blending and set the depth function
			GL.DepthFunc (DepthFunction.Gequal);
			GL.Disable (EnableCap.Blend);

			// Get the shader...
			ActiveShader = FastDrawing ? GetShader ("PerVertexLighting") : GetShader ("PerPixelLighting");

			// Render calls...
			if (model != null) {
			
				// First, render all opaque objects that are not instances
				foreach (var obj in model.DisplayMeshes) {
					if (obj != null)
						RenderObject (obj, viewport, false);
				}

				// Second, render all opaque objects that are instances
				foreach (var obj in model.DisplayInstanceMeshes) {
					if (obj != null)
						RenderObject (obj, viewport, true);
				}

				// Third, we're done drawing our instances, so set the ModelViewMatrix's XForm back to the identity matrix.
				ActiveShader.SetModelViewMatrix (Rhino.Geometry.Transform.Identity);

				// Fourth, render all transparent meshes...
				RenderTransparentObjects (model);
			}

			// Disable the shader
			ActiveShader.Disable ();

			return true;
		}

		/// <summary>
		/// Renders the object in a viewport
		/// </summary>
		private void RenderObject (DisplayObject obj, Rhino.DocObjects.ViewportInfo viewport, bool isInstance)
		{
			// If the object is invisible, return.
			if (!obj.IsVisible)
				return;

			// If the layer that the object is on is turned off, return.
			if (!Model.LayerIsVisibleAtIndex (obj.LayerIndex))
				return;

			DisplayMesh displayMesh = isInstance ? ((DisplayInstanceMesh)obj).Mesh : (DisplayMesh)obj;
			
			if (displayMesh.WillFitOnGPU == false)
				return;

			uint vertex_buffer = 0;

			if (displayMesh != null) {
				// Material setup, if necessary...
				displayMesh.Material.AmbientColor = new [] { 0.0f, 0.0f, 0.0f, 1.0f }; //We want to ignore the ambient color
				if (CurrentMaterial.RuntimeId != displayMesh.Material.RuntimeId)
					ActiveShader.SetupMaterial (displayMesh.Material);
				CurrentMaterial = displayMesh.Material;

				if (displayMesh.VertexBufferHandle == Globals.UNSET_HANDLE) {
				
					displayMesh.LoadDataForVBOs (displayMesh.Mesh);

					// Generate the VertexBuffer
					GL.GenBuffers (1, out vertex_buffer);
					GL.BindBuffer (BufferTarget.ArrayBuffer, vertex_buffer);
					displayMesh.VertexBufferHandle = vertex_buffer;

					// Set the buffer data
					if (!displayMesh.HasVertexNormals && !displayMesh.HasVertexColors) { // Vertices only
						GL.BufferData (BufferTarget.ArrayBuffer, (IntPtr)(displayMesh.Vertices.Length * displayMesh.Stride), displayMesh.Vertices, BufferUsage.StaticDraw);
					} else if (displayMesh.HasVertexNormals && !displayMesh.HasVertexColors) { // VerticesNormals
						GL.BufferData (BufferTarget.ArrayBuffer, (IntPtr)(displayMesh.VerticesNormals.Length * displayMesh.Stride), displayMesh.VerticesNormals, BufferUsage.StaticDraw);
					} else if (!displayMesh.HasVertexNormals && displayMesh.HasVertexColors) { // VerticesColors
						GL.BufferData (BufferTarget.ArrayBuffer, (IntPtr)(displayMesh.VerticesColors.Length * displayMesh.Stride), displayMesh.VerticesColors, BufferUsage.StaticDraw);
					} else if (displayMesh.HasVertexNormals && displayMesh.HasVertexColors) { // VerticesNormalsColors
						GL.BufferData (BufferTarget.ArrayBuffer, (IntPtr)(displayMesh.VerticesNormalsColors.Length * displayMesh.Stride), displayMesh.VerticesNormalsColors, BufferUsage.StaticDraw);
					}

					// If CheckGLError turns up an error (likely an OutOfMemory warning because the mesh won't fit on the GPU)
					// we need to delete the buffers associated with that displayMesh and mark it as too big.
					bool causesGLError = CheckGLError ();
					if (causesGLError) {
						GL.DeleteBuffers (1, ref vertex_buffer);
						displayMesh.WillFitOnGPU = false;
						displayMesh.DeleteIndexBufferObject ();
						GC.Collect ();
					} else {
						displayMesh.WillFitOnGPU = true;
					}

					// Dispose of VBO data after sending to the GPU 
					displayMesh.DeleteVBOData ();

					// If we have drawn all the partitions associated with the underlying openNURBS mesh in the ModelFile, we can delete it...
					if (displayMesh.Mesh.PartitionCount == displayMesh.PartitionIndex + 1)
						Model.ModelFile.Objects.Delete (displayMesh.FileObjectId);

					displayMesh.Mesh = null;
				}

				if (displayMesh.WillFitOnGPU == false)
					return;

				if (displayMesh.IndexBufferHandle == Globals.UNSET_HANDLE) {
					// Index VBO
					uint index_buffer;
					GL.GenBuffers (1, out index_buffer);
					GL.BindBuffer (BufferTarget.ElementArrayBuffer, index_buffer);
					GL.BufferData (BufferTarget.ElementArrayBuffer, (IntPtr)(displayMesh.Indices.Length*sizeof(ushort)), displayMesh.Indices, BufferUsage.StaticDraw);
					displayMesh.IndexBufferHandle = index_buffer;

					// If CheckGLError turns up an error (likely an OutOfMemory warning because the mesh won't fit on the GPU)
					// we need to delete the buffers associated with that displayMesh and mark it as too big.
					bool causesGLError = CheckGLError ();
					if (causesGLError) {
						GL.DeleteBuffers (1, ref vertex_buffer);
						GL.DeleteBuffers (1, ref index_buffer);
						GC.Collect ();
						displayMesh.WillFitOnGPU = false;
					} else {
						displayMesh.WillFitOnGPU = true;
					}

					//Dispose of the index data after sending to the GPU 
					displayMesh.DeleteIndexBufferObject ();
				}

				if (displayMesh.WillFitOnGPU == false)
					return;

				// Vertices
				// ORDER MATTERS...if you don't do things in this order, you will get very frusterated.
				// First, enable the VertexAttribArray for positions
				int rglVertex = ActiveShader.RglVertexIndex;
				GL.EnableVertexAttribArray (rglVertex);
				// Second, Bind the ArrayBuffer
				GL.BindBuffer (BufferTarget.ArrayBuffer, displayMesh.VertexBufferHandle);
				// Third, tell GL where to look for the data...
				GL.VertexAttribPointer (rglVertex, 3, VertexAttribPointerType.Float, false, displayMesh.Stride, IntPtr.Zero);

				CheckGLError ();

				// Normals
				if (displayMesh.HasVertexNormals) {
					int rglNormal = ActiveShader.RglNormalIndex;
					GL.EnableVertexAttribArray (rglNormal);
					GL.VertexAttribPointer (rglNormal, 3, VertexAttribPointerType.Float, false, displayMesh.Stride, (IntPtr)(Marshal.SizeOf (typeof(Rhino.Geometry.Point3f))));

					CheckGLError ();
				}

				// Colors
				if (displayMesh.HasVertexColors) {
					int rglColor = ActiveShader.RglColorIndex;
					GL.EnableVertexAttribArray (rglColor);
					GL.VertexAttribPointer (rglColor, 4, VertexAttribPointerType.Float, false, displayMesh.Stride, (IntPtr)(Marshal.SizeOf (typeof(Rhino.Display.Color4f))));

					CheckGLError ();
				}
					
				if (isInstance) {
					if (!FastDrawing) {
						// Check for inversions on transforms, but only if we are drawing the final "high-quality" frame
						// (because our PerPixel shader does not flip normals based on modelView matrices).
						if ((obj as DisplayInstanceMesh).XForm.Determinant < -Globals.ON_ZERO_TOLERANCE) {
							// an inversion (happens in mirrored instances, etc.)
							// this means the transformation will turn the mesh
							// inside out, so we have to reverse our front face
							// convention.
							GL.FrontFace (FrontFaceDirection.Cw);
						} else {
							GL.FrontFace (FrontFaceDirection.Ccw);
						}
					}

					ActiveShader.SetModelViewMatrix ((obj as DisplayInstanceMesh).XForm);
				}

				// Bind Indices
				GL.BindBuffer (BufferTarget.ElementArrayBuffer, displayMesh.IndexBufferHandle);

				CheckGLError ();

				// Draw...
				#if __ANDROID__
        GL.DrawElements (All.Triangles, displayMesh.IndexBufferLength, All.UnsignedShort, IntPtr.Zero);
				#endif

				#if __IOS__
				CheckGLError();
				GL.DrawElements (BeginMode.Triangles, displayMesh.IndexBufferLength, DrawElementsType.UnsignedShort, IntPtr.Zero);
				#endif
					
				// Disable any and all arrays and buffers we might have used...
				GL.BindBuffer (BufferTarget.ArrayBuffer, displayMesh.VertexBufferHandle);
				GL.DisableVertexAttribArray (ActiveShader.RglColorIndex);
				GL.DisableVertexAttribArray (ActiveShader.RglNormalIndex);
				GL.DisableVertexAttribArray (ActiveShader.RglVertexIndex);

				GL.BindBuffer (BufferTarget.ElementArrayBuffer, displayMesh.IndexBufferHandle);
				GL.BindBuffer (BufferTarget.ArrayBuffer, 0);
				GL.BindBuffer (BufferTarget.ElementArrayBuffer, 0);
			}

		}

		/// <summary>
		/// Draws all transparent meshes
		/// </summary>
		private void RenderTransparentObjects (RMModel model)
		{
			// Drawing transparent meshes is a 3 pass process...
			//
			// Pass #1: With depth buffer writing OFF
			//            i. Draw all objects' backfaces
			//           ii. Draw all "open" objects' front faces.
			//
			// Pass #2: With depth buffer writing ON
			//            i. Draw all objects' front faces
			//
			// Pass #3: With depth buffer writing ON
			//            i. Draw all "open" objects' back faces
			//

			// Provided we actually have a model to render...
			if (model != null) {

				CurrentMaterial = new DisplayMaterial (); //reset to the default (unset) material

				// ... render all transparent meshes...
				if (model.TransparentObjects.Count > 0) {
					//Pass #1
					GL.DepthMask (false);
					GL.Enable (EnableCap.CullFace);

					// i. Draw all objects' backfaces
					GL.CullFace (CullFaceMode.Front);
					foreach (DisplayMesh mesh in model.TransparentObjects) {

						if (mesh != null) {
								RenderObject (mesh, Viewport, false);

							// ii. Draw all "open" objects' front faces.
							if (!mesh.IsClosed) {
								GL.CullFace (CullFaceMode.Back);
								RenderObject (mesh, Viewport, false);
							}
						}
					}

					// Pass #2: Draw all objects' front faces
					GL.DepthMask (true);
					GL.CullFace (CullFaceMode.Back);
					foreach (DisplayMesh mesh in model.TransparentObjects)
						RenderObject (mesh, Viewport, false);

					// Pass #3: Draw all "open" objects' back faces
					GL.CullFace (CullFaceMode.Front);
					foreach (DisplayMesh mesh in model.TransparentObjects) {
						if ((mesh != null) && (!mesh.IsClosed))
							RenderObject (mesh, Viewport, false);
					}

					GL.Disable (EnableCap.CullFace);
				}

				// ...then render all transparent instance meshes...
				if (model.TransparentInstanceObjects.Count > 0) {
					//Pass #1
					GL.DepthMask (false);
					GL.Enable (EnableCap.CullFace);

					// i. Draw all objects' backfaces
					GL.CullFace (CullFaceMode.Front);
					foreach (DisplayInstanceMesh instance in model.TransparentInstanceObjects) {

						if (instance.Mesh != null) {
							RenderObject (instance, Viewport, true);

							// ii. Draw all "open" objects' front faces.
							if (!instance.Mesh.IsClosed) {
								GL.CullFace (CullFaceMode.Back);
								RenderObject (instance, Viewport, true);
							}
						}
					}

					// Pass #2: Draw all objects' front faces
					GL.DepthMask (true);
					GL.CullFace (CullFaceMode.Back);
					foreach (DisplayInstanceMesh instance in model.TransparentInstanceObjects)
						RenderObject (instance, Viewport, true);

					// Pass #3: Draw all "open" objects' back faces
					GL.CullFace (CullFaceMode.Front);
					foreach (DisplayInstanceMesh instance in model.TransparentInstanceObjects) {
						if ((instance.Mesh != null) && (!instance.Mesh.IsClosed))
							RenderObject (instance, Viewport, true);
					}

					GL.Disable (EnableCap.CullFace);
				}
			}
		}

		/// <summary>
		/// IRenderer interface method
		/// Resize should be called on view change events such as device rotation.
		/// </summary>
		public bool Resize()
		{
			return true; // lie about it
		}

		/// <summary>
		/// IRenderer interface method
		/// Clears the openGL context for the next frame.
		/// </summary>
		public bool ClearView()
		{
			// Clear color and depth buffers
			GL.ClearColor (1.0f, 1.0f, 1.0f, 0.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			return true;
		}
		#endregion

		#region Lighting
		/// <summary>
		/// Creates a light that matches the style of the default openNURBS light.
		/// </summary>
		private Rhino.Geometry.Light CreateDefaultLight ()
		{
			Rhino.Geometry.Light light = new Rhino.Geometry.Light ();
			light.IsEnabled = true;
			light.Intensity = 1;
			light.PowerWatts = 0;
			light.LightStyle = Rhino.Geometry.LightStyle.CameraDirectional;
			light.Ambient  = System.Drawing.Color.FromArgb(0,0,0);
			light.Diffuse  = System.Drawing.Color.FromArgb(255, 255, 255);
			light.Specular = System.Drawing.Color.FromArgb(255, 255, 255);
			light.Direction = new Rhino.Geometry.Vector3d(0.0, 0.0, 1.0);
			light.Location = new Rhino.Geometry.Point3d(0.0, 0.0, 0.0);
			light.Length = new Rhino.Geometry.Vector3d(0.0, 0.0, 0.0);
			light.Width = new Rhino.Geometry.Vector3d(0.0, 0.0, 0.0);
			light.SpotAngleRadians = 180.0;
			light.SpotExponent = 0.0;
			light.HotSpot = 1.0;
			light.AttenuationVector = new Rhino.Geometry.Vector3d(1.0, 0.0, 0.0);
			light.SpotLightShadowIntensity = 1.0;

			return light;
		}
		#endregion

		#region Utilities
		/// <summary>
		/// <para>Checks for outstanding GL Errors and logs them to console.</para>
		/// </summary>
		public static bool CheckGLError () 
		{
			#if __IOS__
			var err = GL.GetError ();
			bool hasError = false;
			do {
				if (err != ErrorCode.NoError) {
					#if DEBUG
					Debug.WriteLine ("GL Error: {0}", err.ToString ());
					#endif
					hasError = true;
				} 
				err = GL.GetError ();
			} while ((err != ErrorCode.NoError));
			return hasError;
			#endif

			#if __ANDROID__
			int error = Android.Opengl.GLES20.GlGetError();
			bool hasError = false;
			do {
				if (error != Android.Opengl.GLES20.GlNoError) {
					#if DEBUG
					Debug.WriteLine ("GL Error: " + error.ToString());
					#endif
					hasError = true;
				}
				error = Android.Opengl.GLES20.GlGetError();
			} while ((error != Android.Opengl.GLES20.GlNoError));
			
			return hasError;
			#endif
		}

		/// <summary>
		/// DEBUG only.
		/// Writes supported graphics modes to the console
		/// </summary>
		public static void TestGraphicsModes ()
		{
			#if DEBUG 
			Dictionary<GraphicsMode, GraphicsMode> modes = new Dictionary<GraphicsMode, GraphicsMode>();
			System.Diagnostics.Debug.WriteLine("Cl (RGBA): Color format (total bits and bits per channel).");
			System.Diagnostics.Debug.WriteLine("Dp       : Depth buffer bits.");
			System.Diagnostics.Debug.WriteLine("St       : Stencil buffer bits.");
			System.Diagnostics.Debug.WriteLine("AA       : Sample count for anti-aliasing.");
			System.Diagnostics.Debug.WriteLine("Stereo   : Stereoscoping rendering supported.");
			System.Diagnostics.Debug.WriteLine("");
			System.Diagnostics.Debug.WriteLine("Cl (RGBA), Dp, St, AA, Stereo");
			System.Diagnostics.Debug.WriteLine("-----------------------------");
			foreach (ColorFormat color in new ColorFormat[] { 32, 24, 16, 8 })
				foreach (int depth in new int[] { 24, 16 })
					foreach (int stencil in new int[] { 8, 0 })
						foreach (int samples in new int[] { 0, 2, 4, 6, 8, 16 })
							foreach (bool stereo in new bool[] { false, true })
						{
							try
							{
								GraphicsMode mode = new GraphicsMode(color, depth, stencil, samples, color, 2, stereo);
								if (!modes.ContainsKey(mode))
									modes.Add(mode, mode);
							}
							catch
							{ }
						}

			foreach (GraphicsMode mode in modes.Keys)
				System.Diagnostics.Debug.WriteLine(String.Format("{0}, {1:00}, {2:00}, {3:00}, {4}", mode.ColorFormat, mode.Depth, mode.Stencil, mode.Samples, mode.Stereo));
			#endif
		}
		#endregion

	} 
}