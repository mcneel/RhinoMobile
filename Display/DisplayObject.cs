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
	public class DisplayObject : IDisposable
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
		public virtual bool IsOpaque {  get { return true; } }

		/// <value> Always return 0, must be overridden. </value>
		public virtual uint TriangleCount { get { return 0; } protected set { } }

		/// <value> The DisplayMesh associated with this object. </value>
		public virtual DisplayMesh Mesh { get; protected set; }

		/// <value> The transform of the object. </value>
		public virtual Transform XForm { get; protected set; }
		#endregion

		#region constructors and disposal
		/// <summary>
		/// A DisplayObject is the class that wraps Rhino 3dm document objects for mobile display.
		/// </summary>
		public DisplayObject ()
		{
			GUID = new Guid ();  //new Guid() makes an "empty" all-0 guid (00000000-0000-0000-0000-000000000000) 
		}


		/// <summary>
		/// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
		/// </summary>
		~ DisplayObject () { Dispose (false); }

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
				if (Mesh != null) {
					Mesh.Dispose ();
					Mesh = null;
				}
			}	
		}
		#endregion
	}
}