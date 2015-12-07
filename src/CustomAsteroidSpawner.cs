﻿/** Allows manual control of asteroid detections
 * @file CustomAsteroidSpawner.cs
 * @author %Starstrider42
 * @date Created May 14, 2014
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Starstrider42 {

	namespace CustomAsteroids {
		/** Checks relationship between stock and custom spawners on scene changes
		 */
		internal class SetupSpawner : MonoBehaviour {
			/** Called on the frame when a script is enabled just before any of the Update methods is called the first time.
			 * 
			 * @see[Unity Documentation] (http://docs.unity3d.com/Documentation/ScriptReference/MonoBehaviour.Start.html)
			 * 
			 * @todo What exceptions are thrown by StartCoroutine?
			 */
			public void Start()
			{
				StartCoroutine("editStockSpawner");
				StartCoroutine("confirmCustomSpawner");
			}

			/** This function is called when the object will be destroyed.
			 * 
			 * @see [Unity Documentation] (http://docs.unity3d.com/Documentation/ScriptReference/MonoBehaviour.OnDestroy.html)
			 * 
			 * @todo What exceptions are thrown by StopCoroutine?
			 */
			public void OnDestroy() {
				StopCoroutine("editStockSpawner");
				StopCoroutine("confirmCustomSpawner");
			}

			/** Modifies the stock spawner to match Custom Asteroids settings
			 * 
			 * @return Controls the delay before execution resumes
			 * 
			 * @see [Unity documentation](http://docs.unity3d.com/Documentation/ScriptReference/MonoBehaviour.StartCoroutine.html)
			 * 
			 * @post Asteroid lifetimes match plugin settings
			 * @post If the plugin settings allow a custom spawner, the stock spawner is set to never 
			 * 		create asteroids spontaneously
			 */
			internal System.Collections.IEnumerator editStockSpawner() {
				ScenarioDiscoverableObjects spawner = null;
				do {
					// Testing shows that loop condition is met fast enough that return 0 doesn't hurt performance
					yield return 0;
					// The spawner may be destroyed and re-created before the spawnInterval condition is met... 
					// 	Safer to do the lookup every time
					spawner = CustomAsteroidSpawner.getStockSpawner();
					// Sometimes old scenario persists to when custom addons are reloaded...
					// Check for default value to make sure it's the new one
				} while (spawner == null || spawner.spawnGroupMaxLimit != 8);

				#if DEBUG
				Debug.Log("CustomAsteroids: editing stock spawner...");
				#endif

				spawner.minUntrackedLifetime = AsteroidManager.getOptions().getUntrackedTimes().First;
				spawner.maxUntrackedLifetime = AsteroidManager.getOptions().getUntrackedTimes().Second;

				if (AsteroidManager.getOptions().getCustomSpawner()) {
					// Thou Shalt Not adjust spawnInterval -- it's needed to clean up old asteroids
					spawner.spawnOddsAgainst   = 10000;
					spawner.spawnGroupMinLimit = 0;
					spawner.spawnGroupMaxLimit = 0;
					#if DEBUG
					Debug.Log("CustomAsteroids: stock spawner disabled");
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.spawnGroupMinLimit = " + spawner.spawnGroupMinLimit);
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.spawnGroupMaxLimit = " + spawner.spawnGroupMaxLimit);
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.sizeCurve = " + spawner.sizeCurve.ToString());
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.spawnOddsAgainst = " + spawner.spawnOddsAgainst);
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.spawnInterval = " + spawner.spawnInterval);
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.maxUntrackedLifetime = " + spawner.maxUntrackedLifetime);
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.minUntrackedLifetime = " + spawner.minUntrackedLifetime);
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.spawnGroupMaxLimit = " + spawner.spawnGroupMaxLimit);
					Debug.Log("CustomAsteroids: ScenarioDiscoverableObjects.spawnGroupMinLimit = " + spawner.spawnGroupMinLimit);
					#endif
				}
			}

			/** Ensures the current game has a custom spawner ready for use
			 * 
			 * @return Controls the delay before execution resumes
			 * 
			 * @see [Unity documentation](http://docs.unity3d.com/Documentation/ScriptReference/MonoBehaviour.StartCoroutine.html)
			 * 
			 * @post The currently loaded game has a CustomAsteroidSpawner scenario
			 * 
			 * @note The spawner must be loaded whether or not custom spawning is enabled, in case the 
			 * 		player changes the setting for a later game session
			 */
			internal System.Collections.IEnumerator confirmCustomSpawner() {
				while (HighLogic.CurrentGame.scenarios == null) {
					yield return 0;
				}

				// Does not require ProtoScenarioModule to have an instantiated ScenarioModule
				ProtoScenarioModule curSpawner = HighLogic.CurrentGame.scenarios.
					Find(scenario => scenario.moduleName.Equals(typeof(CustomAsteroidSpawner).Name));

				if (curSpawner == null) {
					Debug.Log("CustomAsteroids: Adding CustomAsteroidSpawner to game '" + HighLogic.CurrentGame.Title + "'");
					HighLogic.CurrentGame.AddProtoScenarioModule(typeof(CustomAsteroidSpawner), 
						GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT);
				}
			}
		}

		/** Class determining when and where asteroids may be spawned
		 * 
		 * @todo Make this class sufficiently generic to be replaceable by third-party implementations
		 */
		public class CustomAsteroidSpawner : ScenarioModule {
			internal CustomAsteroidSpawner() {
				wasEnabled   = AsteroidManager.getOptions().getCustomSpawner();

				// Yay for memoryless distributions -- we don't care how long it's been since an asteroid was detected
				nextAsteroid = Planetarium.GetUniversalTime() + waitForAsteroid();
			}

			/** Update is called every frame, if the MonoBehaviour is enabled.
			 * 
			 * @see [Unity Documentation] (http://docs.unity3d.com/Documentation/ScriptReference/MonoBehaviour.Update.html)
			 * 
			 * Tests whether it's time to create an asteroid
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			public void Update() {
				if(AsteroidManager.getOptions().getCustomSpawner()) {
					ScenarioDiscoverableObjects stockSpawner = getStockSpawner();
					if (stockSpawner == null) {
						#if DEBUG
						Debug.Log("Could not find ScenarioDiscoverableObjects");
						#endif
						return;
					}

					if (Planetarium.GetUniversalTime() > nextAsteroid) {
						// Stock spawner shuts down at high time warps... don't rely on it!
						forceDespawnCheck();

						// More than one asteroid per tick is unlikely even at 100,000×
						while (Planetarium.GetUniversalTime() > nextAsteroid) {
							Debug.Log("CustomAsteroids: asteroid discovered at UT " + nextAsteroid);
							stockSpawner.SpawnAsteroid();

							nextAsteroid += waitForAsteroid();
						}
					}
				}
			}

			/**
			 * Removes any untracked asteroids that have expired. Does not affect actively tracked asteroids or ordinary vessels.
			 */
			private static void forceDespawnCheck() {
				ScenarioDiscoverableObjects stockSpawner = getStockSpawner();

				if (FlightGlobals.Vessels != null) {
					// Not sure if C# lists support concurrent modification; play it safe
					List<Vessel> toDelete = new List<Vessel>();

					foreach (Vessel v in FlightGlobals.Vessels) {
						DiscoveryInfo trackState = v.DiscoveryInfo;
						// This test will fail if and only if v is an unvisited, untracked asteroid
						// It does not matter whether or not it was tracked in the past
						if (trackState != null && !trackState.HaveKnowledgeAbout(DiscoveryLevels.StateVectors)) {
							if (Planetarium.GetUniversalTime() > trackState.fadeUT) {
								toDelete.Add(v);
							}
						}
					}

					foreach (Vessel oldAsteroid in toDelete) {
						Debug.Log("CustomAsteroids: manually removing asteroid " + oldAsteroid.GetName());
						oldAsteroid.Die();
					}
				}
			}

			/** Called when the module is either constructed or loaded as part of a save game
			 * 
			 * @param[in] node The ConfigNode representing this ScenarioModule
			 * 
			 * @pre @p node is assumed to have the following format:
			 * @code{.cfg}
			 * SpawnState
			 * {
			 * 	NextAsteroidUT = 12345.6789
			 * 	Enabled = True
			 * }
			 * @endcode
			 * 
			 * @post The module is initialized with any settings in @p node
			 */
			public override void OnLoad(ConfigNode node)
			{
				base.OnLoad(node);

				#if DEBUG
				Debug.Log("CustomAsteroids: full node = " + node);
				#endif
				ConfigNode thisNode = node.GetNode("SpawnState");
				if (thisNode != null) {
					ConfigNode.LoadObjectFromConfig(this, thisNode);
				}

				// Prevent a backlog of asteroids if the player suddenly switched
				if (!wasEnabled && AsteroidManager.getOptions().getCustomSpawner()) {
					Debug.Log("CustomAsteroids: custom spawner activated, clearing asteroid queue");
					nextAsteroid = Planetarium.GetUniversalTime() + waitForAsteroid();
				}

				// Stored value only needed for preceding block
				wasEnabled = AsteroidManager.getOptions().getCustomSpawner();
			}

			/** Called when the save game including the module is saved
			 * 
			 * @param[out] node The ConfigNode representing this ScenarioModule
			 * 
			 * @post @p node is initialized with the persistent contents of this object
			 * @post @p node has the following format:
			 * @code{.cfg}
			 * SpawnState
			 * {
			 * 	NextAsteroidUT = 12345.6789
			 * 	Enabled = True
			 * }
			 * @endcode
			 */
			public override void OnSave(ConfigNode node)
			{
				base.OnSave(node);

				ConfigNode allData = new ConfigNode();
				ConfigNode.CreateConfigFromObject(this, allData);
				allData.name = "SpawnState";
				node.AddNode(allData);
			}

			/** Returns the time until the next asteroid should be detected
			 * 
			 * @return The number of seconds before an asteroid detection, or infinity if asteroids should not spawn.
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			private static double waitForAsteroid() {
				double rate = AsteroidManager.spawnRate();	// asteroids per day

				if (rate > 0.0) {
					rate /= (24.0 * 3600.0);
					// Waiting time in a Poisson process follows an exponential distribution
					return RandomDist.drawExponential(1.0/rate);
				} else {
					return double.PositiveInfinity;
				}
			}

			/**
			 * Returns the current instance of the stock spawner, if it exists. 
			 * Otherwise, returns null.
			 */
			public static ScenarioDiscoverableObjects getStockSpawner() {
				if (HighLogic.CurrentGame != null) {
					if (HighLogic.CurrentGame.scenarios != null) {
						ProtoScenarioModule protoSpawner = HighLogic.CurrentGame.scenarios
							.Find(scenario => scenario.moduleName.Equals(typeof(ScenarioDiscoverableObjects).Name));
						if (protoSpawner != null) {
							return (ScenarioDiscoverableObjects)protoSpawner.moduleRef;
						}
					}
				}

				return null;
			}

			// TODO: replace with KSPFields?
			/** The time at which the next asteroid will be placed */
			[Persistent(name="NextAsteroidUT")]
			private double nextAsteroid;

			/** Whether the spawner was used previously */
			[Persistent(name="Enabled")]
			private bool wasEnabled;
		}

		/** Workaround to let SetupSpawner be run in multiple specific scenes
		 * 
		 * Shamelessly stolen from Trigger Au, thanks for the idea!
		 * 
		 * Loaded on entering any Flight scene
		 */
		[KSPAddon(KSPAddon.Startup.Flight, false)]
		internal class SSFlight : SetupSpawner {
		}
		/** Workaround to let SetupSpawner be run in multiple specific scenes
		 * 
		 * Shamelessly stolen from Trigger Au, thanks for the idea!
		 * 
		 * Loaded on entering any SpaceCentre scene
		 */
		[KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
		internal class SSSpaceCenter : SetupSpawner {
		}
		/** Workaround to let SetupSpawner be run in multiple specific scenes
		 * 
		 * Shamelessly stolen from Trigger Au, thanks for the idea!
		 * 
		 * Loaded on entering any TrackingStation scene
		 */
		[KSPAddon(KSPAddon.Startup.TrackingStation, false)]
		internal class SSTrackingStation : SetupSpawner {
		}
	}
}
