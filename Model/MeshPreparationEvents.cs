//
// MeshPreparationEvents.cs
// RhinoMobile.Model
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
using RhinoMobile.Model;

namespace RhinoMobile.Model
{
	/// <summary>
	/// Intercepts all MeshPreparationProgress events
	/// </summary>
	public delegate void MeshPreparationHandler(RMModel sender, MeshPreparationProgress e);
	
	// ReSharper disable once InconsistentNaming
	public class MeshPreparationProgress : EventArgs 
	{
		public float MeshProgress { get; set; }
		public bool PreparationDidSucceed { get; set; }
		public Exception FailException { get; set; }
	}
}