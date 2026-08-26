import { useEffect, useMemo, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import {
  chatEducationAssistant,
  generateResponseEducationPackage,
  getIncident,
  getResponseEducationPackage,
} from '../api/incidents';
import type { EducationChatMessage, Incident, ResponseEducationPackageResponse } from '../api/types';
import { ErrorBanner } from '../components/ErrorBanner';
import { IncidentWorkspaceNav } from '../components/IncidentWorkspaceNav';
import { LoadingBlock } from '../components/LoadingBlock';

export function IncidentEducationPage() {
  const { id } = useParams<{ id: string }>();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [responsePack, setResponsePack] = useState<ResponseEducationPackageResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingEducation, setLoadingEducation] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [chatError, setChatError] = useState<string | null>(null);
  const [chatInput, setChatInput] = useState('');
  const [chatBusy, setChatBusy] = useState(false);
  const [selectedQuizAnswers, setSelectedQuizAnswers] = useState<Record<string, number>>({});
  const [submittedQuizAnswers, setSubmittedQuizAnswers] = useState<Record<string, boolean>>({});
  const chatEndRef = useRef<HTMLDivElement | null>(null);
  const [chatHistory, setChatHistory] = useState<EducationChatMessage[]>([
    {
      role: 'assistant',
      content: 'Hi. I am your ARGUS education assistant. Ask me what this incident means and what your team should do next.',
    },
  ]);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [chatHistory]);

  useEffect(() => {
    if (!id) {
      setError('Missing incident id.');
      setLoading(false);
      return;
    }
    const incidentId = id;

    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);
      try {
        setIncident(await getIncident(incidentId));
        try {
          setResponsePack(await getResponseEducationPackage(incidentId));
        } catch {
          setResponsePack(null);
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(loadError instanceof Error ? loadError.message : 'Failed to load education workspace.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [id]);

  const quickPrompts = useMemo(
    () => [
      'Explain this incident in plain language.',
      'What should our staff do first today?',
      'What warning signs should our team remember?',
      'How urgent is this and why?',
    ],
    [],
  );

  async function handleGenerateEducation(): Promise<void> {
    if (!id) {
      return;
    }

    setLoadingEducation(true);
    setError(null);

    try {
      const nextPackage = await generateResponseEducationPackage(id);
      setResponsePack(nextPackage);
      setSelectedQuizAnswers({});
      setSubmittedQuizAnswers({});
    } catch (generateError) {
      setError(generateError instanceof Error ? generateError.message : 'Failed to generate education package.');
    } finally {
      setLoadingEducation(false);
    }
  }

  function handleQuizSelection(questionId: string, optionIndex: number): void {
    setSelectedQuizAnswers((current) => ({
      ...current,
      [questionId]: optionIndex,
    }));
  }

  function handleQuizSubmit(questionId: string): void {
    setSubmittedQuizAnswers((current) => ({
      ...current,
      [questionId]: true,
    }));
  }

  async function sendQuestion(question: string): Promise<void> {
    if (!id || !question.trim()) {
      return;
    }

    const nextUserMessage: EducationChatMessage = { role: 'user', content: question.trim() };
    const updatedHistory = [...chatHistory, nextUserMessage];
    setChatHistory(updatedHistory);
    setChatInput('');
    setChatError(null);
    setChatBusy(true);

    try {
      const response = await chatEducationAssistant(id, {
        question: nextUserMessage.content,
        history: updatedHistory,
      });

      setChatHistory((current) => [
        ...current,
        { role: 'assistant', content: response.answer },
        ...(response.suggestedNextStep
          ? [{ role: 'assistant' as const, content: `Suggested next step: ${response.suggestedNextStep}` }]
          : []),
      ]);
    } catch (assistantError) {
      setChatError(assistantError instanceof Error ? assistantError.message : 'Chat assistant failed.');
    } finally {
      setChatBusy(false);
    }
  }

  async function handleChatSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    await sendQuestion(chatInput);
  }

  if (loading) {
    return <LoadingBlock message="Loading education workspace..." />;
  }

  if (!incident) {
    return (
      <section className="card">
        <h1>Incident Not Found</h1>
      </section>
    );
  }

  const education = responsePack?.package?.education;

  return (
    <section className="space-stack">
      <IncidentWorkspaceNav incidentId={incident.id} />

      <article className="card mission-strip">
        <h1>Education Center</h1>
        <p className="soft-label">Plain-language coaching, warning signs, and an interactive assistant for your team.</p>
      </article>

      {error ? <ErrorBanner message={error} /> : null}

      {!responsePack?.package ? (
        <article className="card space-stack">
          <h2>Generate Education Package</h2>
          <p>Run this once to create incident-specific training content and action guidance.</p>
          <button type="button" className="button-primary" disabled={loadingEducation} onClick={() => void handleGenerateEducation()}>
            {loadingEducation ? 'Generating...' : 'Generate Education Package'}
          </button>
        </article>
      ) : null}

      {responsePack?.package ? (
        <div className="education-layout">
          <article className="card space-stack">
            <h2>Learning Snapshot</h2>
            <p>{responsePack.package.plainLanguageSummary}</p>
            <div className="meta-chip-row">
              <span className="meta-chip">Priority: {responsePack.package.overallPriority}</span>
              <span className="meta-chip">Classification: {responsePack.package.incidentClassification}</span>
            </div>

            {education ? (
              <>
                <div className="card-inner">
                  <h3>{education.title}</h3>
                  <p>{education.explanation}</p>
                  <div className="meta-chip-row">
                    <span className="meta-chip">Audience: {education.audienceLevel}</span>
                    <span className="meta-chip">Time: {education.estimatedMinutes} min</span>
                  </div>
                </div>

                <div className="card-inner">
                  <h3>Warning Signs</h3>
                  <ul className="visual-list">
                    {education.warningSigns.map((sign) => (
                      <li key={sign.title} className="visual-item">
                        <strong>{sign.title}</strong>
                        <p>{sign.explanation}</p>
                      </li>
                    ))}
                  </ul>
                </div>

                {education.takeaways.length > 0 ? (
                  <div className="card-inner">
                    <h3>Key Takeaways</h3>
                    <ul className="visual-list">
                      {education.takeaways.map((takeaway) => (
                        <li key={takeaway} className="visual-item">{takeaway}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}

                {education.questions.length > 0 ? (
                  <div className="card-inner space-stack">
                    <h3>Quick Knowledge Check</h3>
                    <div className="quiz-grid">
                      {education.questions.map((question) => {
                        const selectedAnswer = selectedQuizAnswers[question.id];
                        const wasSubmitted = submittedQuizAnswers[question.id] ?? false;
                        const isCorrect = wasSubmitted && selectedAnswer === question.correctOptionIndex;

                        return (
                          <fieldset key={question.id} className="quiz-card">
                            <legend>
                              <strong>{question.question}</strong>
                            </legend>
                            <div className="quiz-options">
                              {question.options.map((option, optionIndex) => (
                                <label key={`${question.id}-${optionIndex}`} className="quiz-option">
                                  <input
                                    type="radio"
                                    name={question.id}
                                    value={optionIndex}
                                    checked={selectedAnswer === optionIndex}
                                    onChange={() => handleQuizSelection(question.id, optionIndex)}
                                  />
                                  <span>{option}</span>
                                </label>
                              ))}
                            </div>
                            <button
                              type="button"
                              className="button-primary"
                              disabled={selectedAnswer === undefined}
                              onClick={() => handleQuizSubmit(question.id)}
                            >
                              Check Answer
                            </button>
                            {wasSubmitted ? (
                              <p className="quiz-feedback">
                                <strong>{isCorrect ? 'Correct.' : 'Not quite.'}</strong> {question.explanation}
                              </p>
                            ) : null}
                          </fieldset>
                        );
                      })}
                    </div>
                  </div>
                ) : null}
              </>
            ) : null}
          </article>

          <article className="card space-stack chat-panel">
            <div className="chat-header">
              <div>
                <h2>Ask The Assistant</h2>
                <p className="soft-label">Ask questions in everyday language.</p>
              </div>
              <span className="chat-status-dot" aria-label="Assistant online">
                Online
              </span>
            </div>

            <div className="quick-prompts prompt-rail">
              {quickPrompts.map((prompt) => (
                <button
                  key={prompt}
                  type="button"
                  className="prompt-pill"
                  onClick={() => void sendQuestion(prompt)}
                  disabled={chatBusy}
                >
                  {prompt}
                </button>
              ))}
            </div>

            <div className="chat-stream" role="log" aria-live="polite">
              {chatHistory.map((message, index) => (
                <div key={`${message.role}-${index}`} className={`chat-bubble chat-${message.role}`}>
                  <span className="chat-role">{message.role === 'assistant' ? 'ARGUS Assistant' : 'You'}</span>
                  <p>{message.content}</p>
                </div>
              ))}
              {chatBusy ? (
                <div className="chat-bubble chat-assistant chat-typing">
                  <span className="chat-role">ARGUS Assistant</span>
                  <p>Thinking about your question...</p>
                </div>
              ) : null}
              <div ref={chatEndRef} />
            </div>

            {chatError ? <ErrorBanner message={chatError} /> : null}

            <form className="chat-form" onSubmit={(event) => void handleChatSubmit(event)}>
              <label htmlFor="education-chat-input" className="soft-label">Your question</label>
              <div className="chat-composer">
                <textarea
                  id="education-chat-input"
                  value={chatInput}
                  onChange={(event) => setChatInput(event.target.value)}
                  rows={3}
                  placeholder="Ask what this means, what to do next, or how to explain this to staff..."
                />
                <button type="submit" className="button-primary chat-send" disabled={chatBusy || !chatInput.trim()}>
                  {chatBusy ? 'Thinking...' : 'Send'}
                </button>
              </div>
            </form>
          </article>
        </div>
      ) : null}
    </section>
  );
}
