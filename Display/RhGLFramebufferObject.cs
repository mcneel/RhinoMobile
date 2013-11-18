//
// RhGLFramebufferObject.cs
// RhinoMobile.Display
//
// Created by jeff (jeff@mcneel.com) on 11/09/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using OpenTK.Graphics.ES20;

using System;

#region OpenTK-1.0 API diffs
// This region handles differences between OpenTK-1.0  on MonoDroid and MonoTouch.  
// MonoDroid is behind the times and has not yet caught up with MonoTouch
// on the OpenTK-1.0 front.  Once things stabilize, this can be removed. 
// See this thread for details:
// http://forums.xamarin.com/discussion/1939/renderbuffertarget-in-opentk
#if __ANDROID__
using RenderbufferTarget = OpenTK.Graphics.ES20.All;
using RenderbufferInternalFormat = OpenTK.Graphics.ES20.All;
using RenderbufferParameterName = OpenTK.Graphics.ES20.All;
using FramebufferSlot = OpenTK.Graphics.ES20.All;
using FramebufferTarget = OpenTK.Graphics.ES20.All;
using FramebufferParameterName = OpenTK.Graphics.ES20.All;
using FramebufferErrorCode = OpenTK.Graphics.ES20.All;
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
using GetPName = OpenTK.Graphics.ES20.All;
using ErrorCode = OpenTK.Graphics.ES20.All;
#endif
#endregion

namespace RhinoMobile.Display
{
  // Extension constants that don't seem to exist in the current OpenTK, but
  // still seem to work with the core GL implementation...
  enum BufferExtensions : uint
  {
    ReadFramebuffer = 0x8CA8,
    WriteFramebufer = 0x8CA9,
    RenderBufferSamples = 0x8CAB,
  };

  /// <summary>
	/// Simple class that wraps GL render buffers that makes creating, attaching and usage a lot easier...
  /// </summary>
  public class RhGLRenderBuffer
  {
    #region members
    private uint m_handle = Globals.UNSET_HANDLE;
    private bool m_owned = true;
    private int m_samples_used = 0;
    private int m_width = 0;
    private int m_height = 0;

    private static int m_max_samples_found = -1;
    #endregion

    #region constructors
		/// <summary>
		/// Construct a render buffer of given dimensions and format and allocate 
		/// storage accordingly...If nSamples is > 0 then it will allocate a multisample
		/// storage. If unsuccessful, it will dynamically downsize the samples and try
		/// again...repeating the process until it's successful or until samples == 0.
		/// <para>
		/// Note: See SamplesUsed property to determine number of samples that succeeded.
		/// </para>
		/// </summary>
		public RhGLRenderBuffer(int nWidth, int nHeight, All format, int nSamples=0)
    {
			GL.GenRenderbuffers( 1, out m_handle );
			GL.BindRenderbuffer (RenderbufferTarget.Renderbuffer, m_handle);

      // First see if we've already determined the maximum-minimum sample size...
      if ((nSamples > 0) && (m_max_samples_found != -1))
        nSamples = m_max_samples_found;

      // Iterate through all possible sample sizes starting with the given size,
      // and find the maximum-minimum sample size that can exist...
			if (nSamples > 0) {
        int logical_samples = nSamples;
        int actual_samples = 0;
        // ..now iterate until we succeed or until we run out of sample sizes... 
				do {
					#if __IOS__
          GL.Apple.RenderbufferStorageMultisample (All.Renderbuffer, logical_samples, format, nWidth, nHeight);
					GL.GetRenderbufferParameter (RenderbufferTarget.Renderbuffer, (RenderbufferParameterName)BufferExtensions.RenderBufferSamples, out actual_samples);
					#endif

					#if __ANDROID__
					//TODO: Not working on android (yet)
					GL.RenderbufferStorageMultisampleIMG (All.Renderbuffer, logical_samples, format, nWidth, nHeight);
					GL.GetRenderbufferParameter (All.Renderbuffer, All.RenderbufferSamplesImg, out actual_samples);
					#endif

					logical_samples--;
        } while ((actual_samples == 0) && (logical_samples > 0));

        // cache the found maximum-minimum.
        m_max_samples_found = actual_samples;
        m_samples_used = actual_samples;

        // If we failed to find a valid sample size, then just use the standard buffer storage
				if (actual_samples == 0) {
					GL.RenderbufferStorage (RenderbufferTarget.Renderbuffer, (RenderbufferInternalFormat)format, nWidth, nHeight);
				}
         
      } else {
        GL.RenderbufferStorage (RenderbufferTarget.Renderbuffer, (RenderbufferInternalFormat)format, nWidth, nHeight);
      }
      m_width = nWidth;
      m_height = nHeight;
      m_owned = true;
			Console.WriteLine ("SamplesUsed = " + SamplesUsed.ToString ());
    }

		/// <summary>
		/// Construct a buffer from a given handle. This assumes that ALL other buffer attributes
		/// have already been setup and applied...passing in an invalid or uninitialized buffer will
		/// have undetermined results. This mode of operation is primarily used when the internal
		/// system uses/creates its own set of buffer object(s), but we still want to use our
		/// implementation for managing and maintaining render buffers...
		/// <para>
		/// Note: It is assumed that since the passed in render buffer already exists, that any
		///       clean up and/or deallocation of the buffer will be done by the caller.
		/// </para>
		/// </summary>
		public RhGLRenderBuffer(uint fromHandle)
    {
      m_handle = fromHandle;
      GL.BindRenderbuffer (RenderbufferTarget.Renderbuffer, m_handle);
			GL.GetRenderbufferParameter (RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferWidth, out m_width);
			GL.GetRenderbufferParameter (RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferHeight, out m_height);
      m_owned = false;  // don't try to clean up or deallocate this buffer...
    }
		#endregion

    #region operators
    // conversion operator that just makes using render buffers in GL calls simpler.
    public static implicit operator uint(RhGLRenderBuffer rb) { return rb.Handle; }
    #endregion

    #region properties
    public uint Handle
    {
      get { return m_handle; }
      set 
      {
        // changing handles is a destructive operation if/when we currently have a render buffer...
        if ((m_handle != Globals.UNSET_HANDLE) && m_owned )
          GL.DeleteRenderbuffers (1, ref m_handle);
        m_handle = value; 
        GL.BindRenderbuffer (RenderbufferTarget.Renderbuffer, m_handle);
        GL.GetRenderbufferParameter (RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferWidth, out m_width);
        GL.GetRenderbufferParameter (RenderbufferTarget.Renderbuffer, RenderbufferParameterName.RenderbufferHeight, out m_height);
        m_owned = true;
      }
    }

    public int SamplesUsed
    {
      get { return m_samples_used; }
    }

    public int Width {
      get { return m_width; }
    }

    public int Height {
      get { return m_height; }
    }

    #endregion

		#region Utilities
		/// <summary>
		/// DEBUG only.
		/// <para>Checks for outstanding GL Errors and logs them to console.</para>
		/// </summary>
		public static void CheckGLError () 
		{
			#if DEBUG 
			#if __ANDROID__
			var err = GL.GetError ();
			do {
				if (err != ErrorCode.NoError)
					Console.WriteLine ("GL Error: {0}", err.ToString ());
				err = GL.GetError ();
			} while ((err != ErrorCode.NoError));
			#endif
			#endif
		}
		#endregion

  }

  /// <summary>
  /// Framebuffer Object class...
  /// </summary>
  public class RhGLFramebufferObject
  {
    #region members
    private uint m_saved_handle = Globals.UNSET_HANDLE;
    private uint m_handle = Globals.UNSET_HANDLE;
    private bool m_owned = true;
    private bool m_isvalid = false;

    private RhGLRenderBuffer    m_colorBuffer;
    private RhGLRenderBuffer    m_depthBuffer;
    #endregion
    
    #region constructors
		/// <summary>
		/// Constructs a basic frame buffer object with zero render buffers or attachments...
		/// </summary>
		public RhGLFramebufferObject ()
    {
      GL.GenFramebuffers( 1, out m_handle );
      m_owned = true;
    }


		/// <summary>
		/// Construct a frame buffer object with given dimensions. If a color buffer and/or
		/// a depth buffer is desired, then they will also be constructed and attached accordingly.
		/// <para>
		/// Note: You can also attach render buffers post construction by using the ColorBuffer and
		///       DepthBuffer properties.
		/// </para>
		/// </summary>
    public RhGLFramebufferObject(int nWidth, int nHeight, int nSamples, bool bUseColor, bool bUseDepth)
    {
			if (bUseColor) {
				m_colorBuffer = new RhGLRenderBuffer (nWidth, nHeight, All.Rgba8Oes, nSamples);
			}

			if (bUseDepth) {
				m_depthBuffer = new RhGLRenderBuffer (nWidth, nHeight, All.DepthComponent16, nSamples);
			}


      GL.GenFramebuffers( 1, out m_handle );
      m_owned = true;

      if (m_colorBuffer != null || m_depthBuffer != null) 
      {
        Enable ();
          AttachColor ();
          AttachDepth ();
          Validate ();
        Disable ();
      }
    }

		/// <summary>
		/// Construct a frame buffer object from an existing FBO handle. 
		/// <para>
		/// Note: Any existing render buffer attachments to the given FBO handle will also be turned into
		///       render buffer objects, but will not be maintained by this object (i.e. it will not destroy
		///       any buffers that this object does not allocate since it is assumed they're managed by the
		///       caller)
		/// </para>
		/// </summary>
    public RhGLFramebufferObject(uint fromHandle)
    {
      Handle = fromHandle;
      m_owned = false;
    }

    // destroys any/all buffers allocated by this object as well as the FBO handle...
    public void Destroy()
    {
      if ( Enable() )
      {
        // detach any render buffers...
        DetachColor ();
        DetachDepth ();
        Disable ();
        // delete the fbo...
        if ( (m_handle != Globals.UNSET_HANDLE) && m_owned )
          GL.DeleteFramebuffers (1, ref m_handle);  
        m_colorBuffer.Handle = Globals.UNSET_HANDLE;
        m_depthBuffer.Handle = Globals.UNSET_HANDLE;
        m_handle = Globals.UNSET_HANDLE;
        m_owned = true;
        m_isvalid = false;
      }
    }
    #endregion

    #region properties
    public uint Handle
    {
      get { return m_handle; }
      set 
      {
        // assigning a new handle to this FBO is a destructive operation...All render buffers
        // will be detached and destroyed in favor of any that might exist in the given handle.
        Destroy ();

        m_handle = value; 

        if (m_handle != Globals.UNSET_HANDLE) {
          int b = 0;

          Enable ();
            
            // attempt to find a color render buffer attached to the frame buffer...

            GL.GetFramebufferAttachmentParameter (FramebufferTarget.Framebuffer, FramebufferSlot.ColorAttachment0, 
                                                  FramebufferParameterName.FramebufferAttachmentObjectType, out b);
            if (b == (int)RenderbufferTarget.Renderbuffer) 
            {
              GL.GetFramebufferAttachmentParameter (FramebufferTarget.Framebuffer, FramebufferSlot.ColorAttachment0, 
                                                    FramebufferParameterName.FramebufferAttachmentObjectName, out b);
              m_colorBuffer = new RhGLRenderBuffer((uint)b);
            }

            // attempt to find a depth render buffer attached to the frame buffer...
            GL.GetFramebufferAttachmentParameter (FramebufferTarget.Framebuffer, FramebufferSlot.DepthAttachment, 
                                                  FramebufferParameterName.FramebufferAttachmentObjectType, out b);
            if (b == (int)RenderbufferTarget.Renderbuffer) 
            {
              GL.GetFramebufferAttachmentParameter (FramebufferTarget.Framebuffer, FramebufferSlot.DepthAttachment, 
                                                    FramebufferParameterName.FramebufferAttachmentObjectName, out b);
              m_depthBuffer = new RhGLRenderBuffer((uint)b);
            }

            AttachColor ();
            AttachDepth ();
            Validate ();
          Disable ();
        } 
        else // a null handle means a null FBO...which in turn means null render buffers...
        {
          m_colorBuffer.Handle = Globals.UNSET_HANDLE;
          m_depthBuffer.Handle = Globals.UNSET_HANDLE;
        }

        m_owned = true;
      }
    }
    
    public RhGLRenderBuffer ColorBuffer
    {
      get { return m_colorBuffer; }
      set 
      {
        if (Enable ()) 
        {
          DetachColor ();
          if ( m_colorBuffer != null )
            m_colorBuffer.Handle = Globals.UNSET_HANDLE;
          m_colorBuffer = value;
          AttachColor ();
          Validate ();
          Disable ();
        }
      }
    }
    
    public RhGLRenderBuffer DepthBuffer
    {
      get { return m_depthBuffer; }
      set 
      {
        if (Enable ()) 
        {
          DetachDepth ();
          if ( m_depthBuffer != null )
            m_depthBuffer.Handle = Globals.UNSET_HANDLE;
          m_depthBuffer = value;
          AttachDepth ();
          Validate ();
          Disable ();
        }
      }
    }

    public bool IsValid
    {
      get { return m_isvalid; }
    }

    #endregion

    #region methods
    public bool Enable()
    {
      if (m_handle == Globals.UNSET_HANDLE)
        return false;

      int fb = 0;

      GL.GetInteger (GetPName.FramebufferBinding, out fb);
      m_saved_handle = (uint)fb;
      if (m_saved_handle != m_handle)
        GL.BindFramebuffer (FramebufferTarget.Framebuffer, m_handle);
      else
        m_saved_handle = Globals.UNSET_HANDLE;
      return true;
    }

    public void Disable()
    {
      if (m_saved_handle != Globals.UNSET_HANDLE)
        GL.BindFramebuffer (FramebufferTarget.Framebuffer, m_saved_handle);
      else
        GL.BindFramebuffer (FramebufferTarget.Framebuffer, 0);
      m_saved_handle = Globals.UNSET_HANDLE;
    }

    private void Validate()
    {
      FramebufferErrorCode fbec = GL.CheckFramebufferStatus (FramebufferTarget.Framebuffer);
      m_isvalid = (fbec == FramebufferErrorCode.FramebufferComplete);
    }

    private void AttachColor()
    {
      if ( (m_colorBuffer != null) && (m_colorBuffer.Handle != Globals.UNSET_HANDLE) )
        GL.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, 
                                    FramebufferSlot.ColorAttachment0, 
                                    RenderbufferTarget.Renderbuffer, m_colorBuffer);
    }
    private void DetachColor()
    {
      GL.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, 
                                  FramebufferSlot.ColorAttachment0, RenderbufferTarget.Renderbuffer, 0);
    }
    
    private void AttachDepth()
    {
      if ( (m_depthBuffer != null) && (m_depthBuffer.Handle != Globals.UNSET_HANDLE) )
        GL.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, 
                                    FramebufferSlot.DepthAttachment, 
                                    RenderbufferTarget.Renderbuffer, m_depthBuffer);
    }
    private void DetachDepth()
    {
      GL.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, 
                                  FramebufferSlot.DepthAttachment, RenderbufferTarget.Renderbuffer, 0);
    }

    public void CopyTo(RhGLFramebufferObject dstFBO)
    {
      if (dstFBO != this) 
      {
        // use ourself as the source (read) buffer...
        GL.BindFramebuffer ((FramebufferTarget)BufferExtensions.ReadFramebuffer, Handle);

        // use the passed in FBO as the destination (write) buffer...
        GL.BindFramebuffer ((FramebufferTarget)BufferExtensions.WriteFramebufer, dstFBO.Handle);
 
        // perform the copy (blit) operation...
#if __IOS__
				GL.Apple.ResolveMultisampleFramebuffer ();
#endif

#if __ANDROID__
				//TODO: Not yet working on Android...work in progress...
				int  width = 0;
        int  height = 0;

        if ( m_colorBuffer != null )
        {
          width = m_colorBuffer.Width;
          height = m_colorBuffer.Height;
        }
        else if ( m_depthBuffer != null )
        {
          width = m_depthBuffer.Width;
          height = m_depthBuffer.Height;
        }

				// Blitting doesn't seem to exist in the current version of OpenTK1.0 on ES 2.0 for Android
				// For the multisample offscreen rendering, this extension might work:
				// http://www.khronos.org/registry/gles/extensions/IMG/IMG_multisampled_render_to_texture.txt. 
				// It is part of OpenTK-1.0 ES20 API.
			
				// It might be also possible to do the blitting with Android Hardware Composer HAL:
				// http://source.android.com/devices/graphics.html#hwc
				// but MSAA might not be implemented on all devices. It does work on the Nexus 7 though.

				if ((width > 0) && (height > 0)) {
					OpenTK.Graphics.ES30.GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, (int)ClearBufferMask.ColorBufferBit, OpenTK.Graphics.ES30.All.Linear);
				}

#endif

      }
    }

    #endregion

		#region Utilities
		/// <summary>
		/// DEBUG only.
		/// <para>Checks for outstanding GL Errors and logs them to console.</para>
		/// </summary>
		public static void CheckGLError () 
		{
			#if DEBUG 
			#if __ANDROID__
			var err = GL.GetError ();
			do {
				if (err != ErrorCode.NoError)
					Console.WriteLine ("GL Error: {0}", err.ToString ());
				err = GL.GetError ();
			} while ((err != ErrorCode.NoError));
			#endif
			#endif
		}
		#endregion
  }
}