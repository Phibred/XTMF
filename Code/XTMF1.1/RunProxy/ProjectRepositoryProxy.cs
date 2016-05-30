﻿/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.RunProxy
{
    /// <summary>
    /// This project repository is designed to mimic the real project repository during a model
    /// run.  This proxy is required to make sure that multiple model runs all have the proper
    /// active project field to correspond with their executing project.
    /// </summary>
    public class ProjectRepositoryProxy : IProjectRepository
    {
        private IProjectRepository RealRepository;

        public ProjectRepositoryProxy(IProjectRepository realRepository, IProject activeProject)
        {
            RealRepository = realRepository;
            ActiveProject = activeProject;
        }

        public IProject ActiveProject
        {
            get;
            private set;
        }

        public IList<IProject> Projects
        {
            get
            {
                return RealRepository.Projects;
            }
        }

        public bool AddProject(IProject project)
        {
            return RealRepository.AddProject(project);
        }

        public IEnumerator<IProject> GetEnumerator()
        {
            return RealRepository.Projects.GetEnumerator();
        }

        public bool Remove(IProject project)
        {
            return RealRepository.Remove(project);
        }

        public bool RenameProject(IProject project, string newName)
        {
            return RealRepository.RenameProject(project, newName);
        }

        public bool SetDescription(IProject project, string newDescription, ref string error)
        {
            return RealRepository.SetDescription(project, newDescription, ref error);
        }

        public bool ValidateProjectName(string name)
        {
            return RealRepository.ValidateProjectName(name);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (RealRepository as IEnumerable).GetEnumerator();
        }
    }
}
