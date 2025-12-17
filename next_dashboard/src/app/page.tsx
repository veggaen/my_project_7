"use client";

import Link from "next/link";
import Image from "next/image";
import type { CSSProperties } from "react";
import { Reveal } from "./components/motion/Reveal";
import { SplitText } from "./components/motion/SplitText";
import { SiteNavbar } from "./components/SiteNavbar";
import { CORE_FEATURES, SKILLS } from "./lib/content";

const stats = [
  { label: "Skills", value: "23", suffix: "" },
  { label: "Max Level", value: "99", suffix: "" },
  { label: "Total Levels", value: "2,277", suffix: "" },
  { label: "Save Backups", value: "4", suffix: "/player" }
];

export default function Home() {
  return (
    <div className="min-h-screen bg-[#0a0a0f]">
      <SiteNavbar />

      {/* Hero Section */}
      <section className="nav-spacer hero-section min-h-[110vh] flex items-center relative overflow-hidden">
        {/* Background Elements */}
        <div className="absolute top-0 right-0 w-1/2 h-full bg-linear-to-l from-purple-900/10 to-transparent pointer-events-none" />
        <div className="absolute bottom-0 left-0 w-full h-1/2 bg-linear-to-t from-[#0a0a0f] to-transparent pointer-events-none" />

        <div className="page-container grid lg:grid-cols-2 gap-16 items-center relative z-10">
          {/* Left Column: Content */}
          <div className="text-left">
            <Reveal className="inline-flex">
              <div className="inline-flex items-center gap-2 px-4 py-2 bg-purple-500/10 border border-purple-500/30 rounded-full mb-8 backdrop-blur-sm">
                <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></span>
                <span className="text-sm text-purple-300 font-medium">Built on s&box</span>
              </div>
            </Reveal>
            
            <Reveal delay={0.05}>
              <div className="min-w-150"> {/* Prevent wrapping */}
                <SplitText
                  as="h1"
                  text="Vegga Roleplay"
                  className="text-6xl md:text-8xl font-bold mb-6 gradient-text leading-tight tracking-tight whitespace-nowrap"
                  mode="chars"
                  stagger={0.03}
                />
              </div>
            </Reveal>
            
            <Reveal delay={0.15}>
              <SplitText
                as="p"
                text="A modern MMORPG roleplay experience. Featuring 23 skills, custom UI, multiplayer networking, and endless possibilities."
                className="text-xl text-gray-400 max-w-xl mb-10 leading-relaxed"
                mode="words"
                stagger={0.012}
              />
            </Reveal>

            <Reveal delay={0.22}>
              <div className="flex items-center gap-4 flex-wrap">
                <Link 
                  href="/whitepaper"
                  className="btn btn-primary text-lg px-8 py-4 glow-purple hover:scale-105 transition-transform"
                >
                  Read Whitepaper
                </Link>
                <Link 
                  href="/roadmap"
                  className="btn btn-secondary text-lg px-8 py-4 hover:bg-white/5 transition-colors"
                >
                  View Roadmap
                </Link>
              </div>
            </Reveal>
          </div>

          {/* Right Column: Stats Grid */}
          <div className="grid grid-cols-2 gap-6">
            {stats.map((stat, i) => (
              <Reveal key={i} delay={0.3 + i * 0.1} width="100%">
                <div className="card p-8 text-center hover:border-purple-500/30 transition-colors group bg-linear-to-br from-[#111118] to-[#16161f]">
                  <div className="text-4xl md:text-5xl font-bold gradient-text mb-2 group-hover:scale-110 transition-transform duration-300 inline-block">
                    {stat.value}
                  </div>
                  <div className="text-sm text-gray-500 font-medium uppercase tracking-wider">
                    {stat.label}
                    {stat.suffix && <span className="text-gray-600 normal-case ml-1">{stat.suffix}</span>}
                  </div>
                </div>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* Features Grid */}
      <section className="page-section bg-linear-to-b from-transparent to-purple-900/10">
        <div className="page-container">
          <Reveal>
            <h2 className="text-4xl font-bold text-center mb-4">Core Features</h2>
          </Reveal>
          <Reveal delay={0.05}>
            <p className="text-gray-400 text-center mb-12 max-w-2xl mx-auto">
            Everything you need for an immersive roleplay experience
          </p>
          </Reveal>

          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {CORE_FEATURES.map((feature, i) => (
              <Reveal key={i} delay={i * 0.06}>
                <Link
                  href={`/features/${feature.slug}`}
                  className="card cursor-pointer group block"
                >
                  <div className={`w-12 h-12 rounded-xl bg-linear-to-r ${feature.color} flex items-center justify-center text-2xl mb-4 group-hover:scale-110 transition-transform`}>
                    {feature.icon}
                  </div>
                  <h3 className="text-xl font-semibold mb-2">{feature.title}</h3>
                  <p className="text-gray-400">{feature.description}</p>
                </Link>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* Skills Preview */}
      <section className="page-section">
        <div className="page-container">
          <Reveal width="100%">
            <h2 className="text-4xl font-bold text-center mb-4">23 Skills to Master</h2>
          </Reveal>
          <Reveal delay={0.05} width="100%">
            <p className="text-gray-400 text-center mb-14">Progression with authentic XP tables</p>
          </Reveal>

          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 xl:grid-cols-8 gap-4">
            {SKILLS.map((skill, i) => (
              <Reveal key={i} delay={i * 0.015} width="100%">
                <Link
                  href={`/skills/${skill.slug}`}
                  className="card card-skill w-full aspect-square flex flex-col items-center justify-center p-6 hover:scale-105 transition-all duration-300 cursor-pointer group relative overflow-hidden"
                  style={{ "--skill-color": skill.color } as CSSProperties}
                >
                  <div 
                    className="absolute inset-0 opacity-0 group-hover:opacity-10 transition-opacity duration-300"
                    style={{ backgroundColor: "var(--skill-color)" }}
                  />
                  <div 
                    className="text-3xl mb-3 transition-transform duration-300 group-hover:scale-110 group-hover:-translate-y-1"
                    style={{ color: "var(--skill-color)" }}
                  >
                    {skill.icon}
                  </div>
                  <div className="text-sm font-medium text-gray-400 group-hover:text-white transition-colors truncate w-full text-center">
                    {skill.name}
                  </div>
                  <div 
                    className="absolute bottom-0 left-0 w-full h-1 transform scale-x-0 group-hover:scale-x-100 transition-transform duration-300"
                    style={{ backgroundColor: "var(--skill-color)" }}
                  />
                </Link>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="page-section">
        <div className="page-container">
          <Reveal width="100%">
            <div className="flex justify-center">
              <div className="w-full max-w-5xl text-center">
                <div className="card w-full p-12 md:p-16 bg-linear-to-br from-purple-900/20 via-[#111118] to-pink-900/20 border-purple-500/20 relative overflow-hidden group">
                  <div className="absolute inset-0 bg-[url('/grid.svg')] opacity-10" />
                  <div className="absolute -top-24 -right-24 w-48 h-48 bg-purple-500/20 rounded-full blur-3xl group-hover:bg-purple-500/30 transition-colors duration-500" />
                  <div className="absolute -bottom-24 -left-24 w-48 h-48 bg-pink-500/20 rounded-full blur-3xl group-hover:bg-pink-500/30 transition-colors duration-500" />

                  <div className="relative z-10 flex flex-col items-center">
                    <SplitText as="h2" text="Ready to Begin?" className="text-4xl md:text-5xl font-bold mb-6" mode="words" stagger={0.06} />
                    <p className="text-gray-400 text-lg mb-12 max-w-2xl mx-auto leading-relaxed">
                      Dive into the whitepaper to learn about the complete system architecture,
                      or check the roadmap to see what&apos;s coming next.
                    </p>
                    <div className="flex items-center justify-center gap-4 flex-wrap">
                      <Link href="/whitepaper" className="btn btn-secondary min-w-35">
                        Whitepaper
                      </Link>
                      <Link href="/roadmap" className="btn btn-primary min-w-35 glow-purple">
                        Roadmap
                      </Link>
                      <Link href="/stats" className="btn btn-outline min-w-35">
                        Stats
                      </Link>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </Reveal>
        </div>
      </section>

      {/* Footer */}
      <footer className="py-8 px-6 border-t border-white/10">
        <div className="page-container flex items-center justify-between gap-6">
          <div className="flex items-center gap-2">
            <Image
              src="/hexagon_logo.png"
              alt="Vegga Roleplay"
              width={28}
              height={28}
            />
            <span className="text-gray-500">Vegga Roleplay © 2025</span>
          </div>
          <div className="text-gray-500 text-sm">
            Built with ❤️ on s&box
          </div>
        </div>
      </footer>
    </div>
  );
}
