import Image from "next/image";
import Link from "next/link";
import {
  ArrowRight,
  ClipboardList,
  Wrench,
  Package,
  History,
  BarChart3,
  Users,
  ShieldCheck,
  CheckCircle2,
  LockKeyhole,
} from "lucide-react";
import { ContactSection } from "./contact-section";
import { Brand } from "./brand";
import { PublicNavigation } from "./public-navigation";
import styles from "./landing.module.css";

const benefits = [
  [
    ClipboardList,
    "Every request, in one place",
    "Capture the issue, review its priority and follow it through to resolution.",
  ],
  [
    Users,
    "People, coordinated",
    "Assign technicians, plan visits and keep your service team informed.",
  ],
  [
    Wrench,
    "Work that moves forward",
    "Record diagnosis, job notes and checklists throughout the service lifecycle.",
  ],
  [
    Package,
    "Parts with a clear trail",
    "Track receipts, issues, returns and actual consumption against service jobs.",
  ],
  [
    History,
    "Equipment with a history",
    "Find previous repairs, service dates, technicians and parts when the next issue arrives.",
  ],
  [
    BarChart3,
    "Operations in perspective",
    "Review scoped workload, service activity and spare-parts consumption.",
  ],
] as const;
const roles = [
  [
    "Administrator",
    "Manage accounts, permissions, audit records and system oversight.",
  ],
  [
    "Service Manager",
    "Review requests, coordinate technicians and monitor service delivery.",
  ],
  [
    "Technician",
    "Carry out assigned work, record findings and complete service checklists.",
  ],
  [
    "Inventory Staff",
    "Maintain parts records and reconcile stock movements with jobs.",
  ],
  [
    "Customer",
    "Request service, follow your jobs and review your equipment’s history.",
  ],
] as const;
const workflow = [
  "Customer Request",
  "Manager Review",
  "Technician Assignment",
  "Service Execution",
  "Parts Recording",
  "Completion",
  "Customer Confirmation",
  "Reporting",
];

export function Landing() {
  return (
    <div className={styles.page}>
      <a className="skip-link" href="#landing-content">
        Skip to content
      </a>
      <PublicNavigation />
      <main id="landing-content">
        <section id="home" className={styles.hero}>
          <div>
            <p className={styles.eyebrow}>
              <span /> CONNECTED FIELD SERVICE
            </p>
            <h1>
              Smarter Field Service.
              <br />
              <em>Seamless Maintenance.</em>
            </h1>
            <p className={styles.lead}>
              Manage service requests, technicians, work orders, inventory and
              maintenance records from one connected workspace.
            </p>
            <div className={styles.ctas}>
              <Link href="/login" className={styles.primary}>
                Sign In to ServiceOps <ArrowRight size={18} />
              </Link>
              <a href="#features" className={styles.secondary}>
                Explore Features
              </a>
            </div>
            <div className={styles.heroNotes}>
              <span>
                <CheckCircle2 size={16} /> Five connected roles
              </span>
              <span>
                <CheckCircle2 size={16} /> One service lifecycle
              </span>
            </div>
          </div>
          <figure className={styles.heroPreview}>
            <div className={styles.previewBar}>
              <span />
              <span />
              <span />
              <small>ServiceOps / Operations workspace</small>
            </div>
            <Image
              src="/previews/operations-light.webp"
              alt="Verified ServiceOps Administrator dashboard showing the orange and navy interface and fictional demo service records"
              width={1280}
              height={889}
              sizes="(max-width: 900px) 100vw, 55vw"
              preload
            />
            <figcaption>
              Genuine application preview · fictional demonstration data
            </figcaption>
          </figure>
        </section>
        <section id="features" className={styles.section}>
          <p className={styles.eyebrow}>LESS FRAGMENTATION. MORE CLARITY.</p>
          <h2>Built around the way service gets done.</h2>
          <p className={styles.intro}>
            Connect the customer’s first request with the people, parts and
            records needed to finish the job.
          </p>
          <div className={styles.benefits}>
            {benefits.map(([Icon, title, description]) => (
              <article key={title} className={`${styles.card} glass-card`}>
                <span className={styles.icon}>
                  <Icon size={24} />
                </span>
                <h3>{title}</h3>
                <p>{description}</p>
              </article>
            ))}
          </div>
        </section>
        <section
          id="workflow"
          className={`${styles.section} ${styles.workflowSection}`}
        >
          <p className={styles.eyebrow}>ONE CONNECTED JOURNEY</p>
          <h2>From reported issue to recorded repair.</h2>
          <p className={styles.intro}>
            Manager review and approval happen in the assignment action.
            Customer confirmation closes completed work.
          </p>
          <ol className={styles.steps}>
            {workflow.map((step, index) => (
              <li key={step}>
                <span>{String(index + 1).padStart(2, "0")}</span>
                <strong>{step}</strong>
              </li>
            ))}
          </ol>
        </section>
        <section id="modules" className={styles.section}>
          <p className={styles.eyebrow}>THE RIGHT WORKSPACE FOR EVERY ROLE</p>
          <h2>One platform. Five perspectives.</h2>
          <div className={styles.roles}>
            {roles.map(([role, description], index) => (
              <article className={`${styles.roleCard} glass-card`} key={role}>
                <span className={styles.roleNumber}>0{index + 1}</span>
                <h3>{role}</h3>
                <p>{description}</p>
              </article>
            ))}
          </div>
        </section>
        <section className={styles.section} aria-labelledby="preview-heading">
          <p className={styles.eyebrow}>A CLOSER LOOK</p>
          <h2 id="preview-heading">Real tools for everyday operations.</h2>
          <div className={styles.productPreviews}>
            <figure>
              <Image
                src="/previews/history-light.webp"
                alt="ServiceOps Customer service history with a technician, actual demo part consumption and PDF report link"
                width={1280}
                height={889}
                sizes="(max-width: 750px) 100vw, 50vw"
              />
              <figcaption>
                <strong>Maintenance history</strong>
                <span>
                  Repair context and permitted reports, connected to equipment.
                </span>
              </figcaption>
            </figure>
            <figure>
              <Image
                src="/previews/inventory-light.webp"
                alt="ServiceOps Inventory Staff dashboard using genuine fictional demo stock records"
                width={1280}
                height={889}
                sizes="(max-width: 750px) 100vw, 50vw"
              />
              <figcaption>
                <strong>Inventory operations</strong>
                <span>
                  Parts and stock movements with an auditable job connection.
                </span>
              </figcaption>
            </figure>
          </div>
        </section>
        <section id="about" className={`${styles.section} ${styles.about}`}>
          <div>
            <p className={styles.eyebrow}>
              DESIGNED FOR CONTROLLED COLLABORATION
            </p>
            <h2>
              Clear responsibilities.
              <br />
              Records you can follow.
            </h2>
            <p>
              ServiceOps is a university computing project exploring a
              practical, connected field service and maintenance workflow.
            </p>
          </div>
          <div className={styles.security}>
            {[
              [
                ShieldCheck,
                "Role-based access",
                "Server-enforced permissions define who can act.",
              ],
              [
                LockKeyhole,
                "Controlled data access",
                "Customers see their own records; technicians access assigned work.",
              ],
              [
                ClipboardList,
                "Secure authentication and audit records",
                "Hashed passwords, rotating sessions and recorded operational changes.",
              ],
            ].map(([Icon, title, description]) => {
              const Symbol = Icon as typeof ShieldCheck;
              return (
                <article key={String(title)}>
                  <Symbol size={22} />
                  <div>
                    <h3>{String(title)}</h3>
                    <p>{String(description)}</p>
                  </div>
                </article>
              );
            })}
          </div>
        </section>
        <ContactSection />
      </main>
      <footer className={`${styles.footer} glass-footer`}>
        <div className={styles.footerBrand}>
          <Link href="/" aria-label="ServiceOps home"><Brand /></Link>
          <p>ServiceOps connects people, equipment and maintenance operations in one smarter workspace.</p>
        </div>
        <nav aria-label="Footer platform navigation"><h2>Platform</h2>
          <a href="#features">Features</a><a href="#workflow">How It Works</a><a href="#modules">Five connected roles</a>
        </nav>
        <nav aria-label="Footer features navigation"><h2>Operations</h2>
          <a href="#features">Service Management</a><a href="#workflow">Work Orders</a><a href="#features">Equipment</a><a href="#features">Inventory</a>
        </nav>
        <div className={styles.footerStart}><h2>Contact</h2><a href="mailto:contact@serviceops.com">contact@serviceops.com</a><a href="tel:+94777744333">+94 77 774 4333</a><p>Your next service call starts with a connected workspace.</p>
          <Link href="/login" className={styles.smallButton}>Sign In <ArrowRight size={15} /></Link>
        </div>
        <div className={styles.footerBottom}><span>© {new Date().getFullYear()} ServiceOps. All rights reserved.</span><a href="#home">Back to top ↑</a></div>
      </footer>
    </div>
  );
}
