//
// RhGLShaderProgram.cs
// RhinoMobile.Display
//
// Created by dan (dan@mcneel.com) on 9/19/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.Drawing;
using System.Text;

using OpenTK.Graphics.ES20;

using Rhino.Geometry;
using Rhino.DocObjects;

#region OpenTK-1.0 API diffs
// This region handles differences between OpenTK-1.0  on MonoDroid and MonoTouch.  
// MonoDroid is behind the times and has not yet caught up with MonoTouch
// on the OpenTK-1.0 front.  Once things stabilize, this can be removed. 
// See this thread for details:
// http://forums.xamarin.com/discussion/1939/renderbuffertarget-in-opentk
#if __ANDROID__
using RenderBufferTarget = OpenTK.Graphics.ES20.All;
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
#endif
#endregion

namespace RhinoMobile.Display
{
	public struct RhGLPredefinedUniforms
	{
		// ReSharper disable InconsistentNaming
		public int rglModelViewMatrix;
		public int rglProjectionMatrix;
		public int rglNormalMatrix;
		public int rglModelViewProjectionMatrix;

		public int rglDiffuse;
		public int rglSpecular;
		public int rglEmission;
		public int rglShininess;
		public int rglUsesColors;

		public int rglLightAmbient;
		public int rglLightDiffuse;
		public int rglLightSpecular;
		public int rglLightPosition;
		// ReSharper restore InconsistentNaming
	};

	public struct RhGLPredefinedAttributes
	{
		// ReSharper disable InconsistentNaming
		public int rglVertex;
		public int rglNormal;
		public int rglTexCoord0;
		public int rglColor;
		// ReSharper restore InconsistentNaming
	};

	public enum VertexAttributes : int
	{
		AttribVertex,
		AttribNormal,
		AttribTexcoord0,
		AttribColor,
		NumAttributes
	};

	public class RhGLShaderProgram
	{
		#region members
		readonly int m_hProgram;
		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		RhGLPredefinedAttributes m_Attributes;
		RhGLPredefinedUniforms m_Uniforms;

		Transform m_MVXform;
		Transform m_MVPXform;
		#endregion

		/// <summary>
		/// Builds both the vertex and the fragment shaders
		/// Shaders MUST consist of both a vertex AND a fragment shader in 2.0.
		/// </summary>
		public static RhGLShaderProgram BuildProgram (string name, string vertexShader, string fragmentShader)
		{
			if (string.IsNullOrWhiteSpace (vertexShader) || string.IsNullOrWhiteSpace (fragmentShader))
				return null;

			int hVsh = BuildShader (vertexShader, ShaderType.VertexShader);
			int hFsh = BuildShader (fragmentShader, ShaderType.FragmentShader);

			if (hVsh == 0 || hFsh == 0)
				return null;

			int program_handle = GL.CreateProgram ();
			if (program_handle == 0 )
				return null;

			GL.AttachShader (program_handle, hVsh);
			GL.AttachShader (program_handle, hFsh);

			// These bindings are forced here so that mesh drawing can enable the
			// appropriate vertex array based on the same binding values. 
			// Note: These must be done before we attempt to link the program...
			// Note2: Rhino supports multiple textures but for now we'll only
			//        provide for a single set of texture coordinates.
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribVertex, "rglVertex");
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribNormal, "rglNormal");
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribTexcoord0, "rglTexCoord0");
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribColor, "rglColor");

			GL.LinkProgram (program_handle);

			int success;
			GL.GetProgram(program_handle, ProgramParameter.LinkStatus, out success);

			if (success == 0) {
				#if DEBUG
				int logLength;
				GL.GetProgram (program_handle, ProgramParameter.InfoLogLength, out logLength);
				if (logLength > 0) {
					string log = GL.GetProgramInfoLog (program_handle);
					System.Diagnostics.Debug.WriteLine (log);
				}
				#endif

				GL.DetachShader (program_handle, hVsh);
				GL.DetachShader (program_handle, hFsh);

				GL.DeleteProgram (program_handle);
				program_handle = 0;
			}

			GL.DeleteShader (hVsh);
			GL.DeleteShader (hFsh);

			if (program_handle == 0)
				return null;

			RhGLShaderProgram program = new RhGLShaderProgram (name, program_handle);
			program.ResolvePredefines ();

			return program;
		}

		#region properties
		public int Handle
		{
			get { return m_hProgram; }
		}

		public string Name { get; private set; }

		public int RglVertexIndex
		{
			get { return m_Attributes.rglVertex; }
		}

		public int RglNormalIndex
		{
			get { return m_Attributes.rglNormal; }
		}

		public int RglColorIndex
		{
			get { return m_Attributes.rglColor; }
		}

		public int RglNormalMatrix
		{
			get { return m_Uniforms.rglNormalMatrix; }
		}

		public int RglModelViewMatrix
		{
			get { return m_Uniforms.rglModelViewMatrix; }
		}

		public int RglProjectionMatrix
		{
			get { return m_Uniforms.rglProjectionMatrix; }
		}

		public int RglModelViewProjectionMatrix
		{
			get { return m_Uniforms.rglModelViewProjectionMatrix; }
		}
		#endregion

		#region constructors
		/// <summary>
		/// Only allow construction through the static BuildProgram function
		/// </summary>
		private RhGLShaderProgram (string name, int handle)
		{
			Name = name;
			m_hProgram = handle;
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Calls GL.UseProgram and the PreRun method
		/// </summary>
		public void Enable () 
		{
			GL.UseProgram (m_hProgram);
		}

		/// <summary>
		/// Calls PostRun and then sets the program to zero
		/// </summary>
		public void Disable () 
		{
			GL.UseProgram (0);
		}

		/// <summary>
		/// Sets up and initializes the viewport by setting the Uniforms
		/// </summary>
		public void SetupViewport (ViewportInfo viewport) 
		{
			Transform mv = new Transform ();
			bool bHaveModelView = false;

			if (m_Uniforms.rglModelViewProjectionMatrix >= 0) {
	  		Transform mvp = viewport.GetXform (CoordinateSystem.World, CoordinateSystem.Clip);

				m_MVPXform = mvp;
			
				float[] modelViewProjection = mvp.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewProjectionMatrix, 1, false, modelViewProjection);
			}

			if (m_Uniforms.rglModelViewMatrix >= 0) {
				mv = viewport.GetXform (CoordinateSystem.World, CoordinateSystem.Camera);

				m_MVXform = mv;
				bHaveModelView = true;

				float[] modelView = mv.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewMatrix, 1, false, modelView);
			}

			if (m_Uniforms.rglProjectionMatrix >= 0) {
				Transform pr = viewport.GetXform (CoordinateSystem.Camera, CoordinateSystem.Clip);
				float[] projection = pr.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglProjectionMatrix, 1, false, projection);
			}

			if (m_Uniforms.rglNormalMatrix >= 0) {

				float[] normalMatrix = new float[9];

				if (!bHaveModelView) {
					mv = viewport.GetXform (CoordinateSystem.World, CoordinateSystem.Camera);

					m_MVXform = mv;

					mv = mv.Transpose ();
					bHaveModelView = true;
				} 

				Matrix4Dto3F (mv, ref normalMatrix);
				GL.UniformMatrix3 (m_Uniforms.rglNormalMatrix, 1, false, normalMatrix);
			}

		}

		/// <summary>
		/// Given a Rhino light, modifies the uniforms accordingly...
		/// </summary>
		public void SetupLight (Light light)
		{
			if (m_Uniforms.rglLightAmbient >= 0) {
				float[] amb = ConvertColorToFloatArrayOpaque (light.Ambient);
				GL.Uniform4 (m_Uniforms.rglLightAmbient, 1, amb);
			}

			if (m_Uniforms.rglLightDiffuse >= 0) {
				float[] diff = ConvertColorToFloatArrayOpaque (light.Diffuse);
				GL.Uniform4 (m_Uniforms.rglLightDiffuse, 1, diff);
			}
			if (m_Uniforms.rglLightSpecular >= 0) {
				float[] spec = ConvertColorToFloatArrayOpaque (light.Specular);
				GL.Uniform4 (m_Uniforms.rglLightSpecular, 1, spec);
			}
			if (m_Uniforms.rglLightPosition >= 0) {
				float[] pos = {
					(float)light.Direction.X,
					(float)light.Direction.Y,
					(float)light.Direction.Z
				};
				GL.Uniform3 (m_Uniforms.rglLightPosition, 1, pos);
			}
		}

		/// <summary>
		/// Sets up an ES2.0 material with a Rhino material
		/// </summary>
		public void SetupMaterial (Material material)
		{
			Color sColor = material.SpecularColor;

			float alpha = (float)(1.0 - material.Transparency);
			float shine = (float)(128.0 * (material.Shine / Material.MaxShine));
			float[] black = { 0.0f, 0.0f, 0.0f, 1.0f };
			float[] spec = ConvertColorToFloatArray(sColor);
			float[] pspec = Convert.ToBoolean (shine) ? spec : black;

			if (m_Uniforms.rglLightAmbient >= 0) {
				if (material.AmbientColor.A > 0) {
					float[] ambi = ConvertColorToFloatArray(material.AmbientColor);
					GL.Uniform4 (m_Uniforms.rglLightAmbient, 1, ambi);
				} else
					GL.Uniform4 (m_Uniforms.rglLightAmbient, 1, black);
			}

			if (m_Uniforms.rglDiffuse >= 0) {
				Color dColor = material.DiffuseColor;
				float[] convertedColor = ConvertColorToFloatArray(dColor);
				float[] diff = { 
					convertedColor[0],
					convertedColor[1],
					convertedColor[2],
					alpha
				};
				GL.Uniform4 (m_Uniforms.rglDiffuse, 1, diff);
			}
			if (m_Uniforms.rglSpecular >= 0)
				GL.Uniform4 (m_Uniforms.rglSpecular, 1, pspec);
			if (m_Uniforms.rglEmission >= 0) {
				float[] emmi = ConvertColorToFloatArrayOpaque(material.EmissionColor);
				GL.Uniform4 (m_Uniforms.rglEmission, 1, emmi);
			}
			if (m_Uniforms.rglShininess >= 0)
				GL.Uniform1 (m_Uniforms.rglShininess, shine);
			if (m_Uniforms.rglUsesColors >= 0)
				GL.Uniform1 (m_Uniforms.rglUsesColors, 0);

			if (alpha < 1.0)
				GL.Enable (EnableCap.Blend);
			else
				GL.Disable (EnableCap.Blend); 
		}

		/// <summary>
		/// Enables color usage in the shader
		/// </summary>
		public void EnableColorUsage (bool bEnable)
		{
			if (m_Uniforms.rglUsesColors >= 0)
				GL.Uniform1 (m_Uniforms.rglUsesColors, bEnable ? 1 : 0);
		}

		/// <summary>
		/// Pushes a Rhino.Geometry.Transform onto the uniform stack.
		/// </summary>
		public void PushModelViewMatrix (Transform xform)
		{
			Transform mv;

			if (m_Uniforms.rglModelViewProjectionMatrix >= 0) {
				Transform mvp = m_MVPXform;

				mvp = mvp * xform;
				mvp.Transpose ();

				float[] modelViewProjection = mvp.ToFloatArray (false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewProjectionMatrix, 1, false, modelViewProjection);
			}

			if (m_Uniforms.rglModelViewMatrix >= 0) {
				mv = m_MVXform * xform;
				mv.Transpose();

				float[] modelView = mv.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewMatrix,
				                   1,	
				                   false,
				                   modelView);
			}

			if (m_Uniforms.rglNormalMatrix >= 0) {
				float[] normalMatrix = new float[9];

				mv = m_MVXform * xform;
				mv.Transpose();

				Matrix4Dto3F (mv, ref normalMatrix);
				GL.UniformMatrix3 (m_Uniforms.rglNormalMatrix, 1, false, normalMatrix);
			}
		}

		/// <summary>
		/// Pops the current modelViewMatrix off the uniform stack
		/// </summary>
		public void PopModelViewMatrix ()
		{
			Transform mv;
		
			if (m_Uniforms.rglModelViewProjectionMatrix >= 0) {
				Transform mvp = m_MVPXform;
			
				mvp.Transpose ();

				float[] modelViewProjection = mvp.ToFloatArray (false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewProjectionMatrix, 1, false, modelViewProjection);
			}

			if (m_Uniforms.rglModelViewMatrix >= 0) {
				mv = m_MVXform;

				mv.Transpose();

				float[] modelView = mv.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewMatrix,
				                   1,	
				                   false,
				                   modelView);
			}

			if (m_Uniforms.rglNormalMatrix >= 0) {
				float[] normalMatrix = new float[9];
				mv = m_MVXform;
			
				mv.Transpose();

				Matrix4Dto3F(mv, ref normalMatrix);
				GL.UniformMatrix3 (m_Uniforms.rglNormalMatrix, 1, false, normalMatrix);
			}
		}

		#endregion

		#region Protected Methods
		/// <summary>
		/// Compiles the shader source with a type.  In Debug, produces error log if needed.
		/// </summary>
		static int BuildShader (string source, ShaderType type)
		{
			int hShader = GL.CreateShader (type);

			GL.ShaderSource (hShader, source);
			GL.CompileShader (hShader);

			int success;
			GL.GetShader (hShader, ShaderParameter.CompileStatus, out success);

			if (success == 0) {
				#if DEBUG
				int logLength;
				GL.GetShader (hShader, ShaderParameter.InfoLogLength, out logLength);
				if (logLength > 0) {
					string log = GL.GetShaderInfoLog ((int)hShader);
					System.Diagnostics.Debug.WriteLine (log);
				}
				#endif

				GL.DeleteShader (hShader);
				hShader = 0;
			}

			return hShader;
		}

		/// <summary>
		/// Resolves all attributes and uniforms in the shader
		/// </summary>
		public void ResolvePredefines ()
		{
			#if __ANDROID__
			// The following bifurcation is due to differences between OpenTK-1.0 on
			// MonoTouch and MonoDroid.  GL.GetAttribLocation has a different method
			// signature on each platform.  Remove the following in favor of the iOS
			// version when this is corrected by Xamarin.
			StringBuilder rglVertex = new StringBuilder ("rglVertex");
			StringBuilder rglNormal = new StringBuilder ("rglNormal");
			StringBuilder rglTexCoord0 = new StringBuilder ("rglTexCoord0");
			StringBuilder rglColor = new StringBuilder ("rglColor");

			m_Attributes.rglVertex = GL.GetAttribLocation (m_hProgram, rglVertex);
			m_Attributes.rglNormal = GL.GetAttribLocation (m_hProgram, rglNormal);
			m_Attributes.rglTexCoord0 = GL.GetAttribLocation (m_hProgram, rglTexCoord0);
			m_Attributes.rglColor = GL.GetAttribLocation (m_hProgram, rglColor);

			StringBuilder rglModelViewMatrix = new StringBuilder ("rglModelViewMatrix");
			StringBuilder rglProjectionMatrix = new StringBuilder ("rglProjectionMatrix");
			StringBuilder rglNormalMatrix = new StringBuilder ("rglNormalMatrix");
			StringBuilder rglModelViewProjectionMatrix = new StringBuilder ("rglModelViewProjectionMatrix");

			m_Uniforms.rglModelViewMatrix = GL.GetUniformLocation (m_hProgram, rglModelViewMatrix);
			m_Uniforms.rglProjectionMatrix = GL.GetUniformLocation (m_hProgram, rglProjectionMatrix);
			m_Uniforms.rglNormalMatrix = GL.GetUniformLocation (m_hProgram, rglNormalMatrix);
			m_Uniforms.rglModelViewProjectionMatrix = GL.GetUniformLocation (m_hProgram, rglModelViewProjectionMatrix);

			StringBuilder rglDiffuse = new StringBuilder ("rglDiffuse");
			StringBuilder rglSpecular = new StringBuilder ("rglSpecular");
			StringBuilder rglEmission = new StringBuilder ("rglEmission");
			StringBuilder rglShininess = new StringBuilder ("rglShininess");
			StringBuilder rglUsesColors = new StringBuilder ("rglUsesColors");

			m_Uniforms.rglDiffuse = GL.GetUniformLocation (m_hProgram, rglDiffuse);
			m_Uniforms.rglSpecular = GL.GetUniformLocation (m_hProgram, rglSpecular);
			m_Uniforms.rglEmission = GL.GetUniformLocation (m_hProgram, rglEmission);
			m_Uniforms.rglShininess = GL.GetUniformLocation (m_hProgram, rglShininess);
			m_Uniforms.rglUsesColors = GL.GetUniformLocation (m_hProgram, rglUsesColors);

			StringBuilder rglLightAmbient = new StringBuilder ("rglLightAmbient");
			StringBuilder rglLightDiffuse = new StringBuilder ("rglLightDiffuse");
			StringBuilder rglLightSpecular = new StringBuilder ("rglLightSpecular");
			StringBuilder rglLightPosition = new StringBuilder ("rglLightPosition");

			m_Uniforms.rglLightAmbient = GL.GetUniformLocation(m_hProgram, rglLightAmbient);
			m_Uniforms.rglLightDiffuse = GL.GetUniformLocation(m_hProgram, rglLightDiffuse);
			m_Uniforms.rglLightSpecular = GL.GetUniformLocation(m_hProgram, rglLightSpecular);
			m_Uniforms.rglLightPosition = GL.GetUniformLocation(m_hProgram, rglLightPosition);
			#endif

			#if __IOS__
			m_Attributes.rglVertex = GL.GetAttribLocation (m_hProgram, "rglVertex");
			m_Attributes.rglNormal = GL.GetAttribLocation (m_hProgram, "rglNormal");
			m_Attributes.rglTexCoord0 = GL.GetAttribLocation (m_hProgram, "rglTexCoord0");
			m_Attributes.rglColor = GL.GetAttribLocation (m_hProgram, "rglColor");

			m_Uniforms.rglModelViewMatrix = GL.GetUniformLocation (m_hProgram, "rglModelViewMatrix");
			m_Uniforms.rglProjectionMatrix = GL.GetUniformLocation (m_hProgram, "rglProjectionMatrix");
			m_Uniforms.rglNormalMatrix = GL.GetUniformLocation (m_hProgram, "rglNormalMatrix");
			m_Uniforms.rglModelViewProjectionMatrix = GL.GetUniformLocation (m_hProgram, "rglModelViewProjectionMatrix");

			m_Uniforms.rglDiffuse = GL.GetUniformLocation (m_hProgram, "rglDiffuse");
			m_Uniforms.rglSpecular = GL.GetUniformLocation (m_hProgram, "rglSpecular");
			m_Uniforms.rglEmission = GL.GetUniformLocation (m_hProgram, "rglEmission");
			m_Uniforms.rglShininess = GL.GetUniformLocation (m_hProgram, "rglShininess");
			m_Uniforms.rglUsesColors = GL.GetUniformLocation (m_hProgram, "rglUsesColors");

			m_Uniforms.rglLightAmbient = GL.GetUniformLocation(m_hProgram, "rglLightAmbient");
			m_Uniforms.rglLightDiffuse = GL.GetUniformLocation(m_hProgram, "rglLightDiffuse");
			m_Uniforms.rglLightSpecular = GL.GetUniformLocation(m_hProgram, "rglLightSpecular");
			m_Uniforms.rglLightPosition = GL.GetUniformLocation(m_hProgram, "rglLightPosition");
			#endif
		}
		#endregion

		#region Utility Methods
		/// <summary>
		/// Converts a double-precision 4x4 Matrix into a floating point array.
		/// <para>Note: This presumes a row-major format to the matrix...you may need 
		/// to call Transpose() before this conversion to get the correct ordering.</para>
		/// </summary>
		private static void Matrix4Dto3F (Transform d, ref float [] f)
		{
			f[0] =  (float)d[0,0]; f[1]  = (float)d[0,1]; f[2]  = (float)d[0,2]; 
			f[3] =  (float)d[1,0]; f[4]  = (float)d[1,1]; f[5]  = (float)d[1,2]; 
			f[6] =  (float)d[2,0]; f[7]  = (float)d[2,1]; f[8] = (float)d[2,2]; 
		}

		/// <returns> A float array from a System.Drawing.Color </returns>
		private static float[] ConvertColorToFloatArray (System.Drawing.Color color) 
		{
			float red   = (float)color.R / 255.0f;
			float green = (float)color.G / 255.0f;
			float blue  = (float)color.B / 255.0f;
			float alpha = (float)color.A / 255.0f;

			float[] convertedColor = new float[] { red, green, blue, alpha };
			return convertedColor;
		}

		/// <returns> A float array from a System.Drawing.Color that is always opaque </returns>
		private static float[] ConvertColorToFloatArrayOpaque (System.Drawing.Color color) 
		{
			float red   = (float)color.R / 255.0f;
			float green = (float)color.G / 255.0f;
			float blue  = (float)color.B / 255.0f;
			const float alpha = 1.0f;

			float[] convertedColor = new float[] { red, green, blue, alpha };
			return convertedColor;
		}
		#endregion

	}
}