using System;

namespace MineLoot
{
    public sealed partial class MineGame
    {
        private static readonly int[] RatingThresholds={0,10,30,75,150,300,600,1200,2500,5000,10000,20000};
        private static readonly string[] RatingMaterials={"Деревянная","Каменная","Железная","Золотая","Алмазная","Незеритовая"};
        public const int RatingRankCount=12;
        public long RatingBlocks=>Math.Min(CoinLimit,Progress.miningBlocks+Progress.totalBlocks);
        public int Mmr=>RatingScore(RatingBlocks,Progress.earnedCoins,Progress.spentTickets,Progress.spentEnchantingExperience,Progress.prestigeCount);
        public static int RatingScore(long blocks,long coins,long tickets,long xp,int resets)
        {
            // Decimal avoids overflow and preserves the fractional contribution until the final floor.
            decimal activity=(decimal)Math.Max(0,Math.Min(CoinLimit,blocks))*1000+Math.Max(0,Math.Min(CoinLimit,coins))
                +(decimal)Math.Max(0,Math.Min(CoinLimit,tickets))*5000+(decimal)Math.Max(0,Math.Min(CoinLimit,xp))*100;
            return (int)Math.Min(int.MaxValue,decimal.Floor(activity*(1+Math.Max(0,Math.Min(MaxPrestiges,resets)))/100000));
        }
        public static int RatingRank(int score)
        {
            int rank=0;while(rank+1<RatingRankCount && score>=RatingThresholds[rank+1]) rank++;return rank;
        }
        public static int RatingThreshold(int rank)=>RatingThresholds[Math.Max(0,Math.Min(RatingRankCount-1,rank))];
        public static string RatingName(int rank)=>(rank&1)==1?"Зачарованная "+RatingMaterials[Math.Max(0,Math.Min(5,rank/2))].ToLowerInvariant():RatingMaterials[Math.Max(0,Math.Min(5,rank/2))];
        private void SanitizeRating(bool legacy)
        {
            if(legacy)
            {
                // Old saves never recorded gross coins or XP spent. Do not invent them from the wallet.
                Progress.earnedCoins=Progress.spentEnchantingExperience=0;
                Progress.spentTickets=Math.Max(0,Math.Min(10000000,Progress.totalRolls));
                Progress.ratingHistoryPartial=Progress.totalClicks>0||Progress.totalBlocks>0||Progress.totalRolls>0||Progress.prestigeCount>0||Progress.totalProduced>0;
            }
            // Retain the old v16 field for save compatibility, without excluding any profile.
            Progress.ratingTestProfile=false;
            Progress.earnedCoins=Math.Max(0,Math.Min(CoinLimit,Progress.earnedCoins));
            Progress.spentTickets=Math.Max(0,Math.Min(CoinLimit,Progress.spentTickets));
            Progress.spentEnchantingExperience=Math.Max(0,Math.Min(CoinLimit,Progress.spentEnchantingExperience));
        }
    }
}
