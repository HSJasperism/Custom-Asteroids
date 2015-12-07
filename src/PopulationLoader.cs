/** Manages asteroid group configs
 * @file PopulationLoader.cs
 * @author %Starstrider42
 * @date Created October 26, 2014
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Starstrider42 {

	namespace CustomAsteroids {

		/** Stores raw asteroid data
		 * 
		 * @invariant At most one instance of this class exists
		 * 
		 * @todo Clean up this class
		 */
		internal class PopulationLoader {
			/** Creates an empty solar system
			 * 
			 * @post No asteroids will be created
			 * 
			 * @exceptsafe Does not throw exceptions.
			 */
			internal PopulationLoader() {
				asteroidSets = new List<Population>();
				untouchedSet = new DefaultAsteroids();
			}

			/** Factory method obtaining Custom Asteroids settings from a config file
			 * 
			 * @return A newly constructed PopulationLoader object containing a full list
			 * 		of all valid asteroid groups in asteroid config files
			 * 
			 * @exception System.TypeInitializationException Thrown if the PopulationLoader object 
			 * 		could not be constructed
			 * 
			 * @exceptsafe The program is in a consistent state in the event of an exception
			 * 
			 * @todo Can I make Load() atomic?
			 */
			internal static PopulationLoader Load() {
				try {
					// UrlConfig x;
					// x.parent.fullPath;		// Name of file to write to
					// x.config					// AsteroidSet node

					// Start with an empty population list
					PopulationLoader allPops = new PopulationLoader();

					// Search for populations in all config files
					UrlDir.UrlConfig[] configList = GameDatabase.Instance.GetConfigs("AsteroidSets");
					foreach (UrlDir.UrlConfig curSet in configList) {
						foreach (ConfigNode curNode in curSet.config.nodes) {
							if (curNode.name == "ASTEROIDGROUP") {
								try {
									#if DEBUG
									Debug.Log("Customasteroids: ConfigNode '" + curNode + "' loaded");
									#endif
									Population newPop = new Population();
									ConfigNode.LoadObjectFromConfig(newPop, curNode);
									allPops.asteroidSets.Add(newPop);
								} catch (TypeInitializationException e) {
									Debug.LogError("CustomAsteroids: failed to load population '" + curNode.GetValue("name") + "'");
									Debug.LogException(e);
								}	// Attempt to parse remaining populations
							}
							else if (curNode.name == "DEFAULT") {
								try {
									#if DEBUG
									Debug.Log("Customasteroids: ConfigNode '" + curNode + "' loaded");
									#endif
									// Construct-and-swap for better exception safety
									DefaultAsteroids oldPop = new DefaultAsteroids();
									ConfigNode.LoadObjectFromConfig(oldPop, curNode);
									allPops.untouchedSet = oldPop;
								} catch (TypeInitializationException e) {
									Debug.LogError("CustomAsteroids: failed to load population '" + curNode.GetValue("name") + "'");
									Debug.LogException(e);
								}	// Attempt to parse remaining populations
							}
							// ignore any other nodes present
						}
					}

					#if DEBUG
					foreach (Population x in allPops.asteroidSets) {
						Debug.Log("Customasteroids: Population '" + x + "' loaded");
					}
					#endif

					return allPops;
				// No idea what kinds of exceptions are thrown by ConfigNode
				} catch (Exception e) {
					throw new TypeInitializationException("Starstrider42.CustomAsteroids.PopulationLoader", e);
				}
			}

			/** Randomly selects an asteroid population
			 * 
			 * The selection is weighted by the spawn rate of each population; a population with 
			 * 		a rate of 2.0 is twice as likely to be chosen as one with a rate of 1.0.
			 * 
			 * @return A reference to the selected population
			 * 
			 * @exception System.InvalidOperationException Thrown if there are no populations from 
			 * 		which to choose, or if all spawn rates are zero, or if any rate is negative
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			internal Population drawPopulation() {
				try {
					// A typedef! A typedef! My kerbdom for a typedef!
					List<Pair<Population, double>> bins = new List<Pair<Population, double>>();
					bins.Add(new Pair<Population, double>(null, untouchedSet.getSpawnRate()));
					foreach (Population x in asteroidSets) {
						bins.Add(new Pair<Population, double>(x, x.getSpawnRate()));
					}

					return RandomDist.weightedSample(bins);
				} catch (ArgumentException e) {
					throw new InvalidOperationException("CustomAsteroids: could not draw population", e);
				}
			}

			/** Returns the total spawn rate of all asteroid populations.
			 * 
			 * @return The sum of all spawn rates for all populations.
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			internal double getTotalRate() {
				double total = 0.0;
				foreach (Population x in asteroidSets) {
					total += x.getSpawnRate();
				}
				return total;
			}

			/** Returns the name used for asteroids on stock orbits
			 * 
			 * @return The name with which to replace "Ast."
			 * 
			 * @exceptsafe Does not throw exceptions
			 */
			internal string defaultName() {
				return untouchedSet.getAsteroidName();
			}

			/** Debug function for traversing node tree
			 *
			 * @param[in] node The top-level node of the tree to be printed
			 * 
			 * @post The current node and all nodes beneath it are printed, in depth-first order
			 */
			private static void printNode(ConfigNode node) {
				Debug.Log("printNode: NODE = " + node.name);
				foreach (ConfigNode.Value x in node.values) {
					Debug.Log("printNode: " + x.name + " -> " + x.value);
				}
				foreach (ConfigNode x in node.nodes) {
					printNode(x);
				}
			}

			/** The set of loaded asteroid Population objects
			 *
			 * @note Initialized via ConfigNode
			 */
			private List<Population> asteroidSets;
			/** Settings related to stock-like asteroids
			 *
			 * @note Initialized via ConfigNode
			 */
			private DefaultAsteroids untouchedSet;

			/** Contains settings for asteroids that aren't affected by Custom Asteroids
			 */
			private sealed class DefaultAsteroids
			{
				/** Sets default settings for asteroids with unmodified orbits
				 * 
				 * @post The object is initialized to a state in which it will not be expected to generate orbits.
				 * 
				 * @exceptsafe Does not throw exceptions.
				 * 
				 * @note Required by interface of ConfigNode.LoadObjectFromConfig()
				 */
				internal DefaultAsteroids() {
					this.name         = "default";
					this.title        = "Ast.";
					this.spawnRate    = 0.0;
				}

				/** Returns the rate at which stock-like asteroids are discovered
				 * 
				 * @return The rate relative to the rates of all other populations.
				 * 
				 * @exceptsafe Does not throw exceptions.
				 */
				internal double getSpawnRate() {
					return spawnRate;
				}

				/** Returns the name used for stock-like asteroids
				 * 
				 * @return A human-readable string identifying the population. May not be unique.
				 * 
				 * @exceptsafe Does not throw exceptions.
				 */
				internal string getAsteroidName() {
					return title;
				}

				/** Returns a string that represents the current object.
				 *
				 * @return A simple string identifying the object
				 * 
				 * @see [Object.ToString()](http://msdn.microsoft.com/en-us/library/system.object.tostring%28v=vs.90%29.aspx)
				 */
				public override string ToString() {
					return name;
				}

				////////////////////////////////////////////////////////
				// Population properties

				/** The name of the group */
				[Persistent] private string name;
				/** The name of asteroids with unmodified orbits */
				[Persistent] private string title;
				/** The rate, in asteroids per day, at which asteroids appear on stock orbits */
				[Persistent] private double spawnRate;
			}
		}
	}
}
