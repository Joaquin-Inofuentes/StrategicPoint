var a = (SP.Actors.Soldier)System.AppDomain.CurrentDomain.GetData("zz205"); if (a == null) return "sin dato"; return a.DisplayName + " ahora en " + a.transform.position;
