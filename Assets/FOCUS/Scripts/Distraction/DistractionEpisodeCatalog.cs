using System.Collections.Generic;
using UnityEngine;

namespace FocusSim.Distraction
{
    [CreateAssetMenu(menuName = "FOCUS/Distraction/Episode Catalog", fileName = "DistractionEpisodeCatalog")]
    public sealed class DistractionEpisodeCatalog : ScriptableObject
    {
        public List<DistractionEpisodeDefinition> episodes = new List<DistractionEpisodeDefinition>();
    }
}
