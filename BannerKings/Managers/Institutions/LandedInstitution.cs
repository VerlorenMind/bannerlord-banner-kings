﻿using TaleWorlds.CampaignSystem;

namespace BannerKings.Managers.Institutions
{
    public abstract class LandedInstitution : Institution
    {
        protected Settlement settlement;

        protected LandedInstitution(Settlement settlement) : base()
        {
            this.settlement = settlement;
        }

        public Settlement Settlement => this.settlement;
    }
}
