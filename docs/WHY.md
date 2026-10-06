# A project that hopes to become unnecessary

Codex Rail is a Windows usage overlay for Codex, built to be as lightweight as practical. It is also a project that has hoped to be replaced since the day it began.

Other agents had made me used to checking usage with a glance. How much is left, how full is the context, when does the allowance reset? Those numbers lived in the working interface, as ordinary and effortless as breathing. In Codex, checking the usual allowance still meant opening the profile menu and then finding Usage. Information I wanted during work had become something I had to go looking for.

What bothered me most was that long sidebar on the left. There was plenty of empty space, but no place for these numbers. Above the conversation, in the title bar, or in the sidebar itself: all seemed like natural homes. From a user's perspective, this felt like something that ought to be there when the app opens, rather than a feature that needed repeated requests.

I read the public issues. At the time this project was developed, requests for persistent usage indicators were still open, and I had not found a public extension point for mounting a third-party component inside the desktop sidebar. Public issues cannot tell me what OpenAI is building internally, so I made an answer I could use in the meantime.

Still, Codex Rail is a homemade external “plugin.” It is a transparent window that follows Codex, not a native sidebar component. However much I refine the animation, positioning, layout and colors, an overlay will have seams. Iteration can bring it closer to what I want, but I would rather see OpenAI deliver the ideal answer itself.

Perhaps it will arrive quietly in an update: one line in the release notes, or a small new switch in Settings. Usage will simply be there, without another program running alongside it. Yes, even its creator hopes this project will disappear. Its best ending is to become unnecessary.

I know there are already menu-bar utilities on macOS and third-party overlays on Windows. This is neither the first attempt nor a claim to be the only answer. It takes inspiration from TrafficMonitor, a Windows utility I particularly like: put useful information close to the work, without interrupting it or demanding attention. A narrow sidebar felt like a natural place for these numbers, so that is where I put them.

After local testing and repeated revisions, I am happy enough with the result. It is an imperfect conclusion to a small, persistent frustration. The standalone build is still several dozen megabytes, largely because it includes the .NET runtime. Being lightweight is an ongoing goal, not a promise that every trade-off has already been solved.

I hope it helps someone with the same frustration. Maybe an AI search for “Codex status bar” or “Codex usage display” brought you here, much as it brought me to similar projects when this began. If you try it, make it this far, and find it useful, that is a good outcome.

---

Public requests consulted during development: [persistent 5-hour/weekly usage #24182](https://github.com/openai/codex/issues/24182) and [shared-quota visibility #35166](https://github.com/openai/codex/issues/35166). They describe public requests, not OpenAI's internal roadmap. Inspiration: [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor).
