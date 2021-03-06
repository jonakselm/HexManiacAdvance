﻿using HavenSoft.HexManiac.Core.Models.Runs;
using System.Linq;
using Xunit;
namespace HavenSoft.HexManiac.Tests {
   public class ItemEffectTests : BaseViewModelTestClass {
      private readonly PIERun run;
      public ItemEffectTests() {
         run = new PIERun(Model, 0, SortedSpan<int>.None);
      }

      [Fact]
      public void NoLabel_Autocomplete_NoOptions() {
         var options = run.GetAutoCompleteOptions("some text", 0, 9);
         Assert.Empty(options);
      }

      [Fact]
      public void ApplyToFirstPokemon_Autocomplete_BoolOptions() {
         var options = run.GetAutoCompleteOptions("ApplyToFirstPokemonOnly=", 0, 24);

         Assert.Equal("false", options[0].Text);
         Assert.Equal("ApplyToFirstPokemonOnly = true", options[1].LineText);
      }

      [Fact]
      public void Arg_Autocomplete_ArgOptions() {
         var options = run.GetAutoCompleteOptions("Arg=", 0, 4);

         Assert.Equal("LevelUpHealth", options[0].Text);
         Assert.Equal("Half", options[1].Text);
         Assert.Equal("Arg = Max", options[2].LineText);
      }

      [Fact]
      public void General_AutoCompleteBeforeEquals_NoOptions() {
         var options = run.GetAutoCompleteOptions("General = {", 0, 8);
         Assert.Empty(options);
      }

      [Fact]
      public void General_AutoCompleteAfterCurly_RemainingOptions() {
         var options = run.GetAutoCompleteOptions("General = { GuardSpec,  LevelUp }", 0, 23);
         Assert.Equal(4, options.Count);
         Assert.Equal("HealHealth", options[0].Text);
         Assert.Equal("General = { GuardSpec, HealPowerPoints, LevelUp }", options[1].LineText);
      }

      [Fact]
      public void General_AutoCompleteMidWord_FilteredOptions() {
         var options = run.GetAutoCompleteOptions("General = { heal }", 0, 16);
         Assert.Equal(3, options.Count);
         Assert.Equal("HealHealth", options[0].Text);
         Assert.Equal("General = { HealPowerPoints }", options[1].LineText);
      }

      [Fact]
      public void ClearStat_AutoComplete_StatusEffectOptions() {
         var options = run.GetAutoCompleteOptions("ClearStat = {}", 0, 13);
         Assert.Equal(7, options.Count);
         Assert.All(new[] { "Infatuation", "Sleep", "Poison", "Burn", "Ice", "Paralyze", "Confusion" },
            option => Assert.Contains(option, options.Select(o => o.Text)));
      }

      [Fact]
      public void IncreaseStat_AutoComplete_StatOptions() {
         var options = run.GetAutoCompleteOptions("IncreaseStat = {  }", 0, 17);
         Assert.Equal(8, options.Count);
         Assert.All(new[] { "HpEv", "AttackEv", "DefenseEv", "SpecialAttackEv", "SpecialDefenseEv", "SpeedEv", "MaxPowerPoints", "PowerPointsToMax" },
            option => Assert.Contains(option, options.Select(o => o.Text)));
      }

      [Fact]
      public void ChangeHappiness_AutoComplete_RangeOptions() {
         var options = run.GetAutoCompleteOptions("ChangeHappiness = { }", 0, 18);
         Assert.Equal(3, options.Count);
         Assert.All(new[] { "Low", "Mid", "High" },
            option => Assert.Contains(option, options.Select(o => o.Text)));
      }
   }
}
