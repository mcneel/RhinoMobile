//
// DisplayObject.cs
// RhinoMobile.Display
//
// Created by Dan (dan@mcneel.com) on 9/17/2013
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
using Rhino.Geometry;

namespace RhinoMobile.Display
{
	/// <summary>
	/// Parent class for any object that gets displayed
	/// </summary>
	public class DisplayObject
	{
		#region Properties
		/// <value> The identifier of the object in the database. </value>
		public Guid GUID { get; protected set; }

		/// <value> True if the object is selected. </value>
		public bool Selected {get; set;}

		/// <value> True if the object is not hidden. </value>
		public bool IsVisible {get; set;}

		/// <value> The layerIndex of the object in the database. </value>
		public int LayerIndex {get; set;}

		/// <value> True if the object is opaque. </value>
		public bool IsOpaque {  get { return true; } }

		/// <value> Always return 0, must be overridden. </value>
		public uint TriangleCount { get { return 0; } }

		/// <value> The DisplayMesh associated with this object. </value>
		public DisplayMesh Mesh { get; protected set; }

		/// <value> The transform of the object. </value>
		public Transform XForm { get; protected set; }
		#endregion

		#region constructors
		/// <summary>
		/// A DisplayObject is the class that wraps Rhino 3dm document objects for mobile display.
		/// </summary>
		public DisplayObject ()
		{
			GUID = new Guid ();  //new Guid() makes an "empty" all-0 guid (00000000-0000-0000-0000-000000000000) 
		}
		#endregion
	}
}