# WorkElevate ITSM Policy (sample document)

Ye ek demo document hai. Isse upload karke AskMyDocs ko test karo.
Isme jaan-boojhkar aisi baatein hain jo kisi LLM ki training data me nahi ho sakti -
isliye agar jawab sahi aaya, matlab RAG kaam kar raha hai, LLM ki yaadasht nahi.

## SLA aur Priority

Har ticket ko ek priority milti hai aur usi se SLA tay hota hai.

- P1 (Critical): resolution SLA 4 ghante. Poora business ruk gaya ho.
- P2 (High): resolution SLA 8 ghante. Ek team ya ek module atka ho.
- P3 (Medium): resolution SLA 24 ghante. Ek user atka ho, workaround maujood ho.
- P4 (Low): resolution SLA 72 ghante. Cosmetic ya request type ka kaam.

Response SLA alag hota hai: P1 ke liye 15 minute, P2 ke liye 1 ghanta,
P3 ke liye 4 ghante aur P4 ke liye 1 working day.

## Escalation

Agar P1 ticket 2 ghante me bhi "In Progress" state me nahi aata, to wo
automatically Service Delivery Manager ko escalate ho jata hai. Escalation
mail ticket owner, uske manager aur SDM teeno ko jata hai.

Agar koi ticket 3 working days tak "Awaiting User" state me rehta hai, system
use apne aap "Closed - No Response" kar deta hai. User ko closure se 1 din
pehle ek reminder mail jata hai.

## Ticket States

Naya ticket "New" state me banta hai. Assign hone par "Assigned", kaam shuru
hone par "In Progress", user se information maangne par "Awaiting User",
aur solution dene par "Resolved" ho jata hai. Resolved ticket 5 din baad
automatically "Closed" ho jata hai agar user dobara na khole.

## LLM Configuration

WorkElevate me AI features ke liye LLM provider Admin > LLM Settings se
configure hota hai. Teen options milte hain: Gemini, ChatGPT aur WELLM.
Har provider ke liye alag API key aur model name dena hota hai.
Ek waqt par sirf ek provider active rakha ja sakta hai.

## Script Governance

Script Governance page par automation scripts approve ya reject hoti hain.
Reject karte waqt reason likhna mandatory hai - bina reason ke reject button
kaam nahi karta. Approve ki hui script hi end-user devices par chalti hai.

Har script ka ek unique script id hota hai, aur governance flags usi script id
se jude hote hain, ticket id se nahi.

## Access Requests

VPN access ke liye manager approval chahiye. Approval ke baad IT 1 working day
me credentials deta hai. Password reset self-service portal se khud kiya ja
sakta hai - uske liye ticket raise karne ki zaroorat nahi hai.

Admin rights sirf 7 din ke liye temporary milte hain, aur uske liye
Information Security team ki approval lagti hai.
