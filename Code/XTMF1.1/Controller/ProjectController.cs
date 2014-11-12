﻿/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XTMF
{
    public class ProjectController
    {
        private XTMFRuntime Runtime;
        public ProjectController(XTMFRuntime runtime)
        {
            Runtime = runtime;
        }

        /// <summary>
        /// The sessions that are currently running
        /// </summary>
        private List<ProjectEditingSession> EditingSessions = new List<ProjectEditingSession>();

        /// <summary>
        /// The number of sessions per session that is currently running
        /// </summary>
        private List<int> ReferenceCount = new List<int>();

        /// <summary>
        /// This lock needs to be acquired before we are allowed to touch the editing sessions.
        /// </summary>
        private object EditingSessionLock = new object();

        /// <summary>
        /// Get a session to edit a project.
        /// </summary>
        /// <param name="project">The project to edit</param>
        public ProjectEditingSession EditProject(Project project)
        {
            if(project == null)
            {
                throw new ArgumentNullException("project");
            }
            lock (this.EditingSessionLock)
            {
                // First check to see if a session is already open
                for(int i = 0; i < this.EditingSessions.Count; i++)
                {
                    if(this.EditingSessions[i].Project == project)
                    {
                        this.ReferenceCount[i]++;
                        return this.EditingSessions[i];
                    }
                }
                // If we didn't find one create a new reference
                this.EditingSessions.Add(var session = new ProjectEditingSession(project, this.Runtime));
                this.ReferenceCount.Add(1);
                return session;
            }
        }

        /// <summary>
        /// Remove a reference that a session is being edited
        /// </summary>
        /// <param name="session">The session to remove a reference to.</param>
        internal void RemoveEditingReference(ProjectEditingSession session)
        {
            if(session == null)
            {
                throw new ArgumentNullException("session");
            }
            lock (this.EditingSessionLock)
            {
                var index = this.EditingSessions.IndexOf(session);
                if(index < 0)
                {
                    throw new ArgumentException("The session does not exist!", "session");
                }
                this.ReferenceCount[index]--;
                if(this.ReferenceCount[index] <= 0)
                {
                    this.EditingSessions.RemoveAt(index);
                    this.ReferenceCount.RemoveAt(index);
                }
            }
        }

        private bool ValidateProjectName(string name, ref string error)
        {
            if(String.IsNullOrWhiteSpace(name) || !this.Runtime.Configuration.ProjectRepository.ValidateProjectName(name))
            {
                error = "The name is invalid.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Loads a project by name, fails if it doesn't exist already
        /// </summary>
        /// <param name="name">The name of the project to load</param>
        /// <param name="error">Contains an error message in case of failure</param>
        /// <returns>The loaded project</returns>
        public Project Load(string name, ref string error)
        {
            lock (EditingSessionLock)
            {
                var loadedProject = (from project in this.Runtime.Configuration.ProjectRepository.Projects
                                     where project.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)
                                     select project as Project).FirstOrDefault();
                if(loadedProject == null)
                {
                    error = "No project with that name was loaded";
                }
                return loadedProject;
            }
        }

        /// <summary>
        /// Loads a project if it exists or creates a new project with the given name.
        /// </summary>
        /// <param name="name">The name of the project</param>
        /// <param name="error">AContains an error message in case of failure</param>
        /// <returns>The loaded project</returns>
        public Project LoadOrCreate(string name, ref string error)
        {
            lock (EditingSessionLock)
            {
                if((var alreadyLoaded = this.Load(name, ref error)) != null)
                {
                    return alreadyLoaded;
                }
                error = null;
                if(!ValidateProjectName(name, ref error))
                {
                    return null;
                }
                var newProject = new Project(name, this.Runtime.Configuration);
                this.Runtime.Configuration.ProjectRepository.AddProject(newProject);
                return newProject;
            }
        }

        /// <summary>
        /// Gets an editing session for the project
        /// </summary>
        /// <param name="project">The project to create the editing session for</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>The session to edit the project.</returns>
        public ProjectEditingSession EditProject(Project project, ref string error)
        {
            if(project == null)
            {
                throw new ArgumentNullException("project");
            }
            lock (EditingSessionLock)
            {
                // check to see if it already exists
                if((var index = IndexOf(this.EditingSessions, (session) => session.Project == project)) >= 0)
                {
                    this.ReferenceCount[index]++;
                    return this.EditingSessions[index];
                }
                // if it doesn't exist already we need to make a new session
                var newSession = new ProjectEditingSession(project, this.Runtime);
                this.EditingSessions.Add(newSession);
                this.ReferenceCount.Add(1);
                return newSession;
            }
        }

        private static int IndexOf<T>(IList<T> data, Predicate<T> condition)
        {
            var enumerator = data.GetEnumerator();
            for(int i = 0; enumerator.MoveNext(); i++)
            {
                if(condition(enumerator.Current))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Deletes a project
        /// </summary>
        /// <param name="name">The name of the project to delete</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if the project was deleted</returns>
        public bool DeleteProject(string name, ref string error)
        {
            if((var project = this.Load(name, ref string ignore = null)) != null)
            {
                return this.DeleteProject(project, ref error);
            }
            error = "A project with that name was not found.";
            return false;
        }

        /// <summary>
        /// Deletes a project
        /// </summary>
        /// <param name="project">The project to delete</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if the project was deleted, false if it failed.</returns>
        public bool DeleteProject(Project project, ref string error)
        {
            lock (EditingSessionLock)
            {
                if((var index = IndexOf(this.EditingSessions, (session) => session.Project == project)) >= 0)
                {
                    error = "You can not delete a project while it is being edited.";
                    return false;
                }
                return this.Runtime.Configuration.ProjectRepository.Remove(project);
            }
        }

        /// <summary>
        /// Get the list of projects currently active
        /// </summary>
        /// <returns>A snapshot of the projects that are currently active</returns>
        public List<Project> GetProjects()
        {
            lock (this.EditingSessionLock)
            {
                return (from project in this.Runtime.Configuration.ProjectRepository.Projects
                        select project as Project).ToList();
            }
        }
    }
}