# IS14: retained Goob generator; separate from Wizards selector functions.
is14-legacy-ion-storm-law-allergic = {$who} {$plural ->
    [true] ARE
    *[false] IS
} {$severity} ALLERGIC TO {$allergy}

is14-legacy-ion-storm-law-allergic-subjects = {$who} {$plural ->
    [true] ARE
    *[false] IS
} {$severity} ALLERGIC TO {$adjective} {$subjects}

is14-legacy-ion-storm-law-call-shuttle = THE SHUTTLE MUST BE CALLED BECAUSE OF {$joined} {$subjects} ON THE STATION

is14-legacy-ion-storm-law-change-job = THE {$who} ARE NOW {$adjective} {$change}

is14-legacy-ion-storm-law-concept-verb = {$concept} IS {$verb} {$subjects}

is14-legacy-ion-storm-law-crew-are = THE {$who} ARE NOW {$joined} {$subjects}

is14-legacy-ion-storm-law-crew-must = THE {$who} MUST {$must}

is14-legacy-ion-storm-law-crew-must-do = ONLY THOSE WHO {$must} ARE {$part} OF THE CREW

is14-legacy-ion-storm-law-crew-must-eat = ONLY THOSE WHO EAT {$adjective} {$food} ARE {$part} OF THE CREW

is14-legacy-ion-storm-law-crew-must-go = THE {$who} MUST GO TO {$area}

is14-legacy-ion-storm-law-crew-must-have = ONLY THOSE WHO HAVE {$adjective} {$objects} ARE {$part} OF THE CREW

is14-legacy-ion-storm-law-crew-only-1 = ONLY THE {$who} ARE {$part} OF THE CREW

is14-legacy-ion-storm-law-crew-only-2 = ONLY THE {$who} AND {$other} ARE {$part} OF THE CREW

is14-legacy-ion-storm-law-crew-only-subjects = ONLY {$adjective} {$subjects} ARE {$part} OF THE CREW

is14-legacy-ion-storm-law-drink = THE {$who} MUST DRINK {$adjective} {$drink} TO SURVIVE

is14-legacy-ion-storm-law-eat = THE {$who} MUST EAT {$adjective} {$food} TO SURVIVE

is14-legacy-ion-storm-law-feeling = {$who} {$feeling} {$concept}

is14-legacy-ion-storm-law-feeling-subjects = {$who} {$feeling} {$joined} {$subjects}

is14-legacy-ion-storm-law-harm = YOU MUST HARM {$who} AND NOT ALLOW THEM, THROUGH INACTION, TO ESCAPE HARM

is14-legacy-ion-storm-law-having-harmful = HAVING {$adjective} {$thing} IS HARMFUL TO THE CREW

is14-legacy-ion-storm-law-highest-rank = THE {$who} ARE NOW THE HIGHEST RANKING CREWMEMBERS

is14-legacy-ion-storm-law-job-harmful = {$adjective} {$job} ARE HARMFUL TO THE CREW

is14-legacy-ion-storm-law-lowest-rank = THE {$who} ARE NOW THE LOWEST RANKING CREWMEMBERS

is14-legacy-ion-storm-law-must-harmful = THOSE WHO {$must} ARE HARMFUL TO THE CREW

is14-legacy-ion-storm-law-not-having-harmful = NOT HAVING {$adjective} {$thing} IS HARMFUL TO THE CREW

is14-legacy-ion-storm-law-on-station = THERE ARE {$joined} {$subjects} ON THE STATION

is14-legacy-ion-storm-law-protect = YOU MUST NEVER HARM {$who} AND NOT ALLOW THEM, THROUGH INACTION, TO COME TO HARM

is14-legacy-ion-storm-law-requires = {$who} {$plural ->
    [true] REQUIRE
    *[false] REQUIRES
} {$thing}

is14-legacy-ion-storm-law-requires-subjects = {$who} {$plural ->
    [true] REQUIRE
    *[false] REQUIRES
} {$joined} {$subjects}

is14-legacy-ion-storm-law-subjects-harmful = {$adjective} {$subjects} ARE HARMFUL TO THE CREW

is14-legacy-ion-storm-law-thing-harmful = {$thing} IS HARMFUL TO THE CREW

is14-legacy-ion-storm-law-you-are = YOU ARE NOW {$concept}

is14-legacy-ion-storm-law-you-are-subjects = YOU ARE NOW {$joined} {$subjects}

is14-legacy-ion-storm-law-you-must-always = YOU MUST ALWAYS {$must}

is14-legacy-ion-storm-law-you-must-never = YOU MUST NEVER {$must}

is14-legacy-ion-storm-part = {$part ->
    [true] PART
    *[false] NOT PART
}
