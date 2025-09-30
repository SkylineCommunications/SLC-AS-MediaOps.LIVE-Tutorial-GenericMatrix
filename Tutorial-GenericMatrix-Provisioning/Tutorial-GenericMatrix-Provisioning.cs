using System;
using System.Collections.Generic;
using System.Linq;

using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Core.DataMinerSystem.Common;
using Skyline.DataMiner.MediaOps.Live.API;
using Skyline.DataMiner.MediaOps.Live.API.Enums;
using Skyline.DataMiner.MediaOps.Live.API.Objects.ConnectivityManagement;
using Skyline.DataMiner.MediaOps.Live.Automation;
using Skyline.DataMiner.MediaOps.Live.Extensions;

namespace TutorialGenericMatrixProvisioning
{
	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			var api = engine.GetMediaOpsLiveApi();
			var element = engine.FindElement("Matrix");

			ProvisionSources(engine, element, api);
			ProvisionDestinations(engine, element, api);
		}

		private void ProvisionSources(IEngine engine, Element element, MediaOpsLiveApi api)
		{
			var elementId = new DmsElementId(element.DmaId, element.ElementId);

			// Get the video level and SDI transport type. They will assigned to the endpoints and virtual signal groups.
			var videoLevel = api.Levels.Read("Video");
			var sdiTransportType = api.TransportTypes.Read("SDI");

			// Get existing endpoints and virtual signal groups for the element.
			var existingEndpoints = api.Endpoints.Query()
				.Where(x => x.Role == Role.Source && x.Element == elementId)
				.SafeToDictionary(x => (x.Element, x.Identifier));

			var existingVirtualSignalGroups = api.VirtualSignalGroups
				.GetByEndpointIds(existingEndpoints.Values.Select(x => x.ID))
				.SafeToDictionary(x => x.Name);

			engine.GenerateInformation($"Found {existingEndpoints.Count} existing endpoints and {existingVirtualSignalGroups.Count} existing virtual signal groups for matrix inputs.");

			// Prepare lists to hold the new or updated endpoints and virtual signal groups.
			var newEndpoints = new List<Endpoint>();
			var newVirtualSignalGroups = new List<VirtualSignalGroup>();

			foreach (var key in element.GetTablePrimaryKeys("Router Control Inputs"))
			{
				var name = $"Matrix Input {key}";

				// Create the endpoint if it does not exist yet.
				if (!existingEndpoints.TryGetValue((elementId, key), out var endpoint))
				{
					endpoint = new Endpoint();
				}

				endpoint.Name = name;
				endpoint.Role = Role.Source;
				endpoint.Element = elementId;
				endpoint.Identifier = key;
				endpoint.TransportType = sdiTransportType;

				newEndpoints.Add(endpoint);

				// Create the virtual signal group if it does not exist yet.
				if (!existingVirtualSignalGroups.TryGetValue(name, out var vsg))
				{
					vsg = new VirtualSignalGroup();
				}

				vsg.Name = name;
				vsg.Role = Role.Source;
				vsg.AssignEndpointToLevel(videoLevel, endpoint);

				newVirtualSignalGroups.Add(vsg);
			}

			// Create or update the endpoints and virtual signal groups via the API.
			api.Endpoints.CreateOrUpdate(newEndpoints);
			api.VirtualSignalGroups.CreateOrUpdate(newVirtualSignalGroups);
		}

		private void ProvisionDestinations(IEngine engine, Element element, MediaOpsLiveApi api)
		{
			var elementId = new DmsElementId(element.DmaId, element.ElementId);

			var videoLevel = api.Levels.Read("Video");
			var sdiTransportType = api.TransportTypes.Read("SDI");

			var existingEndpoints = api.Endpoints.Query()
				.Where(x => x.Role == Role.Destination && x.Element == elementId)
				.SafeToDictionary(x => (x.Element, x.Identifier));

			var existingVirtualSignalGroups = api.VirtualSignalGroups
				.GetByEndpointIds(existingEndpoints.Values.Select(x => x.ID))
				.SafeToDictionary(x => x.Name);

			engine.GenerateInformation($"Found {existingEndpoints.Count} existing endpoints and {existingVirtualSignalGroups.Count} existing virtual signal groups for matrix outputs.");

			var newEndpoints = new List<Endpoint>();
			var newVirtualSignalGroups = new List<VirtualSignalGroup>();

			foreach (var key in element.GetTablePrimaryKeys("Router Control Outputs"))
			{
				var name = $"Matrix Output {key}";

				// Create the endpoint if it does not exist yet.
				if (!existingEndpoints.TryGetValue((elementId, key), out var endpoint))
				{
					endpoint = new Endpoint();
				}

				endpoint.Name = name;
				endpoint.Role = Role.Destination;
				endpoint.Element = elementId;
				endpoint.Identifier = key;
				endpoint.TransportType = sdiTransportType;

				newEndpoints.Add(endpoint);

				// Create the virtual signal group if it does not exist yet.
				if (!existingVirtualSignalGroups.TryGetValue(name, out var vsg))
				{
					vsg = new VirtualSignalGroup();
				}

				vsg.Name = name;
				vsg.Role = Role.Destination;
				vsg.AssignEndpointToLevel(videoLevel, endpoint);

				newVirtualSignalGroups.Add(vsg);
			}

			api.Endpoints.CreateOrUpdate(newEndpoints);
			api.VirtualSignalGroups.CreateOrUpdate(newVirtualSignalGroups);
		}
	}
}
