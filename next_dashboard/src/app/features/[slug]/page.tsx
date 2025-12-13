import Link from "next/link";
import { notFound } from "next/navigation";
import { SiteNavbar } from "../../components/SiteNavbar";
import { Reveal } from "../../components/motion/Reveal";
import { SplitText } from "../../components/motion/SplitText";
import { getFeatureBySlug } from "../../lib/content";

export default function FeaturePage({ params }: { params: { slug: string } }) {
  const feature = getFeatureBySlug(params.slug);
  if (!feature) return notFound();

  return (
    <main className="min-h-screen bg-[#0a0a0f]">
      <SiteNavbar />

      <div className="nav-spacer page-section">
        <div className="page-container max-w-6xl">
          <Reveal>
            <div className="mb-8 text-center">
              <SplitText
                as="h1"
                text={feature.title}
                className="text-4xl md:text-5xl font-bold tracking-tight"
                mode="words"
                stagger={0.06}
              />
              <p className="mt-3 text-gray-300 max-w-2xl mx-auto">{feature.description}</p>
            </div>
          </Reveal>

          <div className="grid lg:grid-cols-3 gap-6">
            <div className="lg:col-span-1">
              <div className="card">
                <div className={`w-14 h-14 rounded-2xl bg-linear-to-r ${feature.color} flex items-center justify-center text-3xl mb-4`}>
                  {feature.icon}
                </div>
                <div className="text-sm text-gray-400">Status</div>
                <div className="text-lg font-semibold">Placeholder</div>

                <div className="mt-6 flex flex-wrap gap-2">
                  <Link href="/" className="btn btn-secondary">
                    Back Home
                  </Link>
                  <Link href="/roadmap" className="btn btn-outline">
                    Roadmap
                  </Link>
                </div>
              </div>
            </div>

            <div className="lg:col-span-2 space-y-6">
              <Reveal>
                <section className="card">
                  <h2 className="text-xl font-semibold mb-2">How it works (Coming Soon)</h2>
                  <p className="text-gray-400">
                    This page will explain the system design, data flow, and how this feature connects to the rest of Project 7.
                  </p>
                </section>
              </Reveal>

              <Reveal delay={0.05}>
                <section className="card">
                  <h2 className="text-xl font-semibold mb-2">Implementation notes (Coming Soon)</h2>
                  <p className="text-gray-400">
                    Planned: endpoints, save schema, UI surfaces, and any constraints/lessons learned.
                  </p>
                </section>
              </Reveal>

              <Reveal delay={0.1}>
                <section className="card">
                  <h2 className="text-xl font-semibold mb-2">Milestones (Coming Soon)</h2>
                  <p className="text-gray-400">
                    Planned: what counts as “done” for MVP vs polished.
                  </p>
                </section>
              </Reveal>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
}
