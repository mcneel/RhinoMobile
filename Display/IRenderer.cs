//
// IRenderer.cs
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
using RhinoMobile.Model;
using Rhino.DocObjects;

namespace RhinoMobile.Display
{
	public struct RhGLDrawable {
		public uint VertexBuffer;
		public uint IndexBuffer;
		public int IndexCount;
		public int Attrs;
	}

	/// <summary>
	/// Defines the rendering interface for all renderer classes.
	/// </summary>
	public interface IRenderer
	{
		bool RenderModel (RMModel model, ViewportInfo viewport);
		bool Resize();
		bool ClearView();
	}
}