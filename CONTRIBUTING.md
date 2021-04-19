# Contributing to PugetSound

## Before starting

This project is adopting the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/), with one obvious change: any concerns should come to *cocnotice at tomzorz dot me*.

## Core design philosophies 

As you can read in the main readme, I had a few concerns with how other similar solutions work. Because of this, I designed PugetSound with a certain principles in mind:

- the site doesn't have to be open to participate in a room
- don't play music from the website (this also helps with legal issues)
- songs come from the queues in a round robin fashion
- no registration/account needed for use

## Discussion before major pull requests

**If you're intending to implement any major feature or refactoring, please open a discussion issue first and outline the work you intend to do - this is to avoid you coding a lot and me deciding that it goes against some of the design philosophies or my general vision for the project.** 

I'm not completely against changing the way the site works, but it'd definitely require a discussion. As an example, I've had requests for having a room controlled by a single shared queue playlist, that can be also modified by a discord bot. I could be open to implementing that as a separate room type, but as soon as we do that there are various hurdles:

- we will probably need to have accounts
- we need 3-way auth between discord, spotify and pugetsound
- not everyone has their spotify linked to their discord
- have to rework the architecture a bit, to support multiple room types and room abstractions in general