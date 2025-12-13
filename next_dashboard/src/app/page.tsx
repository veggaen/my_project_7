"use client";

import Link from "next/link";
import Image from "next/image";
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
      <section className="nav-spacer hero-section">
        <div className="page-container text-center">
          <Reveal className="inline-flex justify-center">
            <div className="inline-flex items-center gap-2 px-4 py-2 bg-purple-500/10 border border-purple-500/30 rounded-full mb-8">
            <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></span>
            <span className="text-sm text-purple-300">Built on s&box</span>
            </div>
          </Reveal>
          
          <Reveal delay={0.05}>
            <SplitText
              as="h1"
              text="Vegga Roleplay"
              className="text-6xl md:text-7xl font-bold mb-6 gradient-text"
              mode="chars"
              stagger={0.02}
            />
          </Reveal>
          
          <Reveal delay={0.15}>
          <p className="text-xl text-gray-400 max-w-2xl mx-auto mb-12">
            A modern MMORPG roleplay experience built on s&box.
            Featuring 23 skills, custom UI, multiplayer networking, and endless possibilities.
          </p>
          </Reveal>

          <Reveal delay={0.22}>
          <div className="flex items-center justify-center gap-3 mb-16 flex-wrap">
            <Link 
              href="/whitepaper"
              className="btn btn-primary text-base md:text-lg glow-purple"
            >
              Read Whitepaper
            </Link>
            <Link 
              href="/roadmap"
              className="btn btn-secondary text-base md:text-lg"
            >
              View Roadmap
            </Link>
          </div>
          </Reveal>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6 max-w-3xl mx-auto">
            {stats.map((stat, i) => (
              <Reveal key={i} delay={0.1 + i * 0.05}>
                <div className="card text-center">
                  <div className="text-3xl font-bold gradient-text">{stat.value}</div>
                  <div className="text-sm text-gray-500">{stat.label}{stat.suffix}</div>
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
          <Reveal>
            <h2 className="text-4xl font-bold text-center mb-4">23 Skills to Master</h2>
          </Reveal>
          <Reveal delay={0.05}>
            <p className="text-gray-400 text-center mb-14">Progression with authentic XP tables</p>
          </Reveal>

          <div className="grid grid-cols-4 md:grid-cols-6 lg:grid-cols-8 gap-4">
            {SKILLS.map((skill, i) => (
              <Reveal key={i} delay={i * 0.015}>
                <Link
                  href={`/skills/${skill.slug}`}
                  className="card text-center p-4 hover:scale-105 transition-transform cursor-pointer block"
                  style={{ borderColor: skill.color + "40" }}
                >
                  <div className="text-2xl mb-2">{skill.icon}</div>
                  <div className="text-xs text-gray-400 truncate">{skill.name}</div>
                </Link>
              </Reveal>
            ))}
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="page-section">
        <div className="page-container">
          <Reveal>
          <div className="max-w-6xl mx-auto text-center">
          <div className="card p-10 md:p-14 bg-linear-to-r from-purple-900/30 to-pink-900/30 border-purple-500/30">
            <SplitText as="h2" text="Ready to Begin?" className="text-4xl font-bold mb-4" mode="words" stagger={0.06} />
            <p className="text-gray-400 mb-8 max-w-xl mx-auto">
              Dive into the whitepaper to learn about the complete system architecture, 
              or check the roadmap to see what&apos;s coming next.
            </p>
            <div className="flex items-center justify-center gap-3 flex-wrap">
              <Link 
                href="/whitepaper"
                className="btn btn-secondary"
              >
                Whitepaper
              </Link>
              <Link 
                href="/roadmap"
                className="btn btn-primary"
              >
                Roadmap
              </Link>
              <Link 
                href="/stats"
                className="btn btn-outline"
              >
                Stats
              </Link>
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
