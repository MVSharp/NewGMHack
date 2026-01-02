using MessagePack;
using System;

namespace NewGMHack.Stub;

[AttributeUsage(AttributeTargets.Field)]
public class FeatureMetadataAttribute : Attribute
{
    public string DisplayNameEn { get; }
    public string DisplayNameCn { get; }
    public string DisplayNameTw { get; }
    public string Description { get; }

    public FeatureMetadataAttribute(string en, string cn, string tw, string desc = "")
    {
        DisplayNameEn = en;
        DisplayNameCn = cn;
        DisplayNameTw = tw;
        Description = desc;
    }
}

public enum FeatureName
{
    [FeatureMetadata("Mission Bomb", "任务炸弹", "任務炸彈", "Instantly complete missions")]
    IsMissionBomb,

    [FeatureMetadata("Player Bomb", "全场炸弹", "全場炸彈", "Attack all players")]
    IsPlayerBomb,

    //IsRandomLocation,

    [FeatureMetadata("Auto Ready", "自动准备", "自動準備", "Automatically ready up in lobby")]
    IsAutoReady,

    [FeatureMetadata("Reflect Damage", "反彈傷害", "反彈傷官", "reflect damages to sources")]
    IsRebound,

    [FeatureMetadata("Illusion", "別天神", "別天神", "Invincibility (Illusion)")]
    IsIllusion,

    //[FeatureMetadata("Aim Assist", "自瞄辅助", "自瞄輔助", "Help aiming at targets")]
    //IsAimSupport,

    [FeatureMetadata("Auto Charge", "自動充電", "自動充電", "auto charge battery for machine")]
    IsAutoCharge,

    [FeatureMetadata("Suck Star Over China", "吸星大法", "吸星大法", "Pull enemies together")]
    SuckStarOverChina,

    [FeatureMetadata("Auto Funnel", "自动浮游炮", "自動浮游砲", "Automatically launch funnels")]
    IsAutoFunnel,

    [FeatureMetadata("Auto Gift", "自动领奖", "自動領獎", "Collect daily rewards automatically")]
    CollectGift,

    [FeatureMetadata("ESP Overlay", "透视", "透視", "Show enemy info on screen")]
    EnableOverlay,

    [FeatureMetadata("Auto Aim", "自动瞄准", "自動瞄準", "Automatically lock on targets")]
    EnableAutoAim,

    [FeatureMetadata("Debug Mode", "调试模式", "調試模式", "Show debug info")]
    Debug,

    [FeatureMetadata("Free Move", "自由移动", "自由移動", "Move freely")]
    FreeMove,

    [FeatureMetadata("Static Process Mode", "後台模式", "後台模式", "Used to reduce resources when you left the game behind")]
    BackGroundMode,
    
    [FeatureMetadata("Freeze Enemy", "冷凍敵人", "冷凍離人", "Freeze all the enemy")]
    FreezeEnemy,
}