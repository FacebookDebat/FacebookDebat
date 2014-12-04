module BasicClassifier

#if INTERACTIVE
#r @".\BasicClassifier\LSA.dll"
let nielsen = @".\BasicClassifier\Nielsen2010.txt"
#endif
open LSA
open System.IO
open System

let Stem sentence = 
    LSA.WordCleaner.Clean sentence

let ParseScorelist scorelist =
    let raw = File.ReadAllLines(scorelist)
    let rawlineCast (rawline:string) =
        let parts = rawline.Split('\t')
        (parts.[0], int parts.[1])
    let pairs = raw |> Seq.map rawlineCast
    pairs

let StemScores scoretuples =
    scoretuples |> Seq.map (fun (word:string,score) -> 
                                match word.Contains(" ") with
                                |true -> (word,score)
                                |false -> (Stem word |> Seq.exactlyOne,score))

let ScoreDictionary raw =
    let  dict = new System.Collections.Generic.Dictionary<string,int>()
    let tryAdd (key,value) = if dict.ContainsKey(key) then () else dict.Add(key,value) 
    raw |> Seq.iter tryAdd
    raw |> StemScores |> Seq.iter tryAdd
    dict

let Score (dict:Collections.Generic.Dictionary<string,int>) (sentence:string) =
    let ScoreWord word =
        match dict.TryGetValue(word) with
        | true, x -> x
        | _ -> match dict.TryGetValue(Stem word|>Seq.exactlyOne) with
                | true, x -> x
                | _ -> 0
    sentence.Split(' ') |> Array.map string |> Array.sumBy ScoreWord


type Classifier(scorelist) =
    member this.Dictionary = ParseScorelist scorelist |> ScoreDictionary
    member this.Score(sentence) = Score this.Dictionary sentence 